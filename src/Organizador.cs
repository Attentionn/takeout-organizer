using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32.SafeHandles;

internal static partial class Organizador
{
    static readonly StringComparer CI = StringComparer.OrdinalIgnoreCase;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(true);
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static readonly string Base = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string Planes = Path.Combine(Base, "Planes");
    static readonly HashSet<string> Fotos = Exts("jpg jpeg jpe jfif heic heif png webp gif bmp tif tiff dng arw cr2 cr3 nef nrw orf rw2 raf pef avif jxl mpo");
    static readonly HashSet<string> Videos = Exts("mp4 mov m4v avi mkv webm mpg mpeg mts m2ts 3gp 3g2 wmv flv vob ts mod");
    static readonly HashSet<string> Anexos = Exts("json xmp aae thm");
    static readonly Regex Captura = new Regex(@"screen[\s_.-]*(?:shots?|caps?|captures?|record(?:ing|er)?s?)|capturas?(?:[\s_.-]+de)?[\s_.-]+pantalla|grabaci[oó]n de pantalla|snipping tool", RegexOptions.IgnoreCase);
    static readonly Regex Facebook = new Regex(@"(?:^|[^a-z])(?:facebook|fb[_-](?:img|vid))(?=$|[^a-z])|(?:^|\.)fbcdn\.", RegexOptions.IgnoreCase);
    static string T(string es,string en) { return Idioma.Texto(es,en); }
    static HashSet<string> Exts(string extensions) { return new HashSet<string>(extensions.Split(' ').Select(x => "."+x), CI); }

    public class Config
    {
        public string[] Origenes {get;set;}
        public string Destino {get;set;}
        public string SHA256 {get;set;}
        public string Layout {get;set;}
    }
    class Anexo
    {
        public FileInfo Archivo;
        public string Sufijo;
        public Fecha Google;
        public bool PorBase;
        public string DestinoDe(string media)
        {
            return PorBase ? Path.ChangeExtension(media,Sufijo) : media+Sufijo;
        }
    }
    static string Full(string p)
    {
        string full = Path.GetFullPath(p);
        return full.Length == Path.GetPathRoot(full).Length ? full : full.TrimEnd('\\');
    }
    static bool Dentro(string p, string root)
    {
        return CI.Equals(p, root) || p.StartsWith(root.TrimEnd('\\')+"\\", StringComparison.OrdinalIgnoreCase);
    }
    static void Validar(Config cfg)
    {
        if (cfg.Origenes.Length == 0) throw new IOException(T("Falta una carpeta de origen.","Choose at least one source folder."));
        var roots = cfg.Origenes.Concat(new[]{cfg.Destino}).Select(Full).ToArray();
        for (int i=0; i<roots.Length; i++)
        {
            if (!CI.Equals(Path.GetPathRoot(roots[i]), Path.GetPathRoot(cfg.Destino)))
                throw new IOException(T("Origen y destino deben usar la misma unidad o el mismo recurso compartido.","Sources and destination must use the same drive or network share."));
            for (int j=0; j<i; j++)
                if (Dentro(roots[i], roots[j]) || Dentro(roots[j], roots[i])) throw new IOException(T("Las carpetas deben ser independientes.","Choose separate folders. One folder cannot contain another."));
            if (Dentro(Planes,roots[i]) || Dentro(roots[i],Planes)) throw new IOException(T("La carpeta de planes debe estar fuera de la biblioteca.","The plans folder must be outside the library."));
            SinEnlaces(roots[i]);
        }
    }
    static void SinEnlaces(string path)
    {
        for (string p=path; p!=null; p=Path.GetDirectoryName(p))
        {
            try
            {
                if ((File.GetAttributes(Win32Path(p)) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(T("Enlace o punto de montaje: ","Link or mount point: ")+p);
            }
            catch (FileNotFoundException) {}
            catch (DirectoryNotFoundException) {}
        }
    }
    static string Hash(string file)
    {
        using (var sha=SHA256.Create())
        using (var input=File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
    }
    static void Csv(TextWriter writer, params string[] fields)
    {
        writer.WriteLine(string.Join(",", fields.Select(s => "\""+s.Replace("\"","\"\"")+"\"")));
    }
    static string[] Componentes(string path, string root)
    {
        return path.Substring(root.Length).Split(new[]{'\\'}, StringSplitOptions.RemoveEmptyEntries);
    }
    static readonly Regex NombreHex = new Regex(@"^[0-9a-f]{32}(?: ?\(\d+\))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static string Categoria(string nombre, string[] partes, bool video)
    {
        var pistas = partes.Concat(new[]{nombre});
        bool fb = pistas.Any(p => Facebook.IsMatch(p));
        bool cap = pistas.Any(p => Captura.IsMatch(p) || CI.Equals(p,"Capturas"));
        if (video && !fb && !cap && (NombreHex.IsMatch(Path.GetFileNameWithoutExtension(nombre)) ||
            partes.Any(p=>CI.Equals(p,"Posibles TikToks") || CI.Equals(p,"Possible TikToks"))))
            return Path.Combine("Videos",T("Posibles TikToks","Possible TikToks"));
        string cat = video ? "Videos" : cap ? T("Capturas","Screenshots") : fb ? "Facebook" : T("Fotos","Photos");
        if (video && cap) cat = Path.Combine(cat,T("Capturas","Screenshots"));
        return fb && (video || cap) ? Path.Combine(cat,"Facebook") : cat;
    }
    static bool CarpetaTipo(string p)
    {
        return Regex.IsMatch(p, @"^(facebook|screenshots?|capturas(?: de pantalla)?|fotos|photos|videos|im[aá]genes|posibles tiktoks|possible tiktoks)$", RegexOptions.IgnoreCase);
    }
    static string RutaFecha(string layout, string anio, int mes, DateTime? dia)
    {
        if (layout=="type") return "";
        if (anio==null) return T("Sin fecha","Undated");
        if (layout=="year") return anio;
        if (mes==0) return Path.Combine(anio,T("Mes desconocido","Unknown month"));
        string month=mes.ToString("00")+" - "+Idioma.Cultura.DateTimeFormat.GetMonthName(mes);
        string path=Path.Combine(anio,month);
        return layout=="day" && dia.HasValue ? Path.Combine(path,dia.Value.ToString("yyyy-MM-dd")) : path;
    }
    // Read only the leading date/category structure. Subsequent directories belong to albums.
    class RutaAnterior
    {
        public string Anio, GrupoManual, Categoria;
        public int Mes;
        public DateTime? Dia;
        public string[] Album;
        static readonly Regex MesNombrado = new Regex(@"^(0[1-9]|1[0-2]) - (\p{L}+)$");
        static readonly string[] NombresMes =
            "enero febrero marzo abril mayo junio julio agosto septiembre octubre noviembre diciembre".Split(' ');
        static readonly string[] MonthNames = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;
        static int NumeroMes(string p)
        {
            var m=MesNombrado.Match(p);
            if (!m.Success) return 0;
            int n=int.Parse(m.Groups[1].Value);
            return CI.Equals(m.Groups[2].Value,NombresMes[n-1]) || CI.Equals(m.Groups[2].Value,MonthNames[n-1]) ? n : 0;
        }
        public static RutaAnterior Leer(string[] p, bool video)
        {
            if (p.Length==0) return null;
            var r=new RutaAnterior();
            int i=0;
            if (Regex.IsMatch(p[0],@"^\d{4}$")) r.Anio=p[i++];
            else if (CI.Equals(p[0],"Sin fecha") || CI.Equals(p[0],"Undated")) i++;
            else if (p.Length>2 && NumeroMes(p[1])!=0) r.GrupoManual=p[i++];
            else return null;
            if (i>0 && i<p.Length)
            {
                r.Mes=NumeroMes(p[i]);
                if (r.Mes!=0)
                {
                    i++;
                    DateTime day;
                    if (i<p.Length && DateTime.TryParseExact(p[i],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)
                        && day.Month==r.Mes && (r.Anio==null || day.ToString("yyyy")==r.Anio))
                    { r.Dia=day; i++; }
                }
                else if (CI.Equals(p[i],"Mes desconocido") || CI.Equals(p[i],"Unknown month")) i++;
            }
            if (i>=p.Length) return null;
            bool videos=CI.Equals(p[i],"Videos");
            if (videos!=video) return null;
            if (videos) r.Categoria="Videos";
            else if (CI.Equals(p[i],"Fotos") || CI.Equals(p[i],"Photos")) r.Categoria=T("Fotos","Photos");
            else if (CI.Equals(p[i],"Capturas") || CI.Equals(p[i],"Screenshots")) r.Categoria=T("Capturas","Screenshots");
            else if (CI.Equals(p[i],"Facebook")) r.Categoria="Facebook";
            else return null;
            i++;
            if (videos && i<p.Length && (CI.Equals(p[i],"Posibles TikToks") || CI.Equals(p[i],"Possible TikToks")))
                { r.Categoria=Path.Combine(r.Categoria,T("Posibles TikToks","Possible TikToks")); i++; }
            else
            {
                if (videos && i<p.Length && (CI.Equals(p[i],"Capturas") || CI.Equals(p[i],"Screenshots")))
                    { r.Categoria=Path.Combine(r.Categoria,T("Capturas","Screenshots")); i++; }
                if (i<p.Length && CI.Equals(p[i],"Facebook") && (videos || r.Categoria==T("Capturas","Screenshots")))
                    { r.Categoria=Path.Combine(r.Categoria,"Facebook"); i++; }
            }
            r.Album=p.Skip(i).ToArray();
            return r;
        }
    }
    class Planificador : IDisposable
    {
        readonly Config cfg;
        readonly HashSet<string> reservados = new HashSet<string>(CI);
        readonly StreamWriter plan, avisos;
        readonly Dictionary<string,int> fuentes = new Dictionary<string,int>();
        int total, incidencias, adjuntos;
        public Planificador(Config config, string dir)
        {
            cfg=config;
            plan=new StreamWriter(Path.Combine(dir,"Plan.csv"),false,Utf8);
            avisos=new StreamWriter(Path.Combine(dir,"Revisar.csv"),false,Utf8);
            Csv(plan,T("Origen","Source"),T("Destino","Destination"),T("Tipo","Type"),T("Fuente de fecha","Date source"),"Bytes",T("Modificado UTC ticks","Modified UTC ticks"));
            Csv(avisos,T("Archivo o carpeta","File or folder"),T("Motivo","Reason"));
        }
        void Aviso(string path, string motivo) { Csv(avisos,path,motivo); incidencias++; }
        void Anotar(FileInfo f, string target, string cat, string fuente)
        {
            Csv(plan,f.FullName,target,cat,fuente,f.Length.ToString(),f.LastWriteTimeUtc.Ticks.ToString());
            reservados.Add(target);
            total++;
        }
        bool Libre(string p) { return !reservados.Contains(p) && !Existe(p); }
        Dictionary<string,List<Anexo>> Asociar(FileInfo[] medios, IEnumerable<FileInfo> otros)
        {
            var porNombre = medios.ToDictionary(f=>f.Name,CI);
            var porBase = medios.ToLookup(f=>Path.GetFileNameWithoutExtension(f.Name),CI);
            var grupos = new Dictionary<string,List<Anexo>>(CI);
            foreach (var f in otros)
            {
                if (!Anexos.Contains(f.Extension)) { Aviso(f.FullName,T("Formato sin regla. Permanece en origen.","Unsupported format. Left in source.")); continue; }
                bool json = CI.Equals(f.Extension,".json");
                string stem = Path.GetFileNameWithoutExtension(f.Name);
                string key = json ? Regex.Replace(stem,@"\.supplemental-metadata$","",RegexOptions.IgnoreCase) : stem;
                FileInfo medio;
                porNombre.TryGetValue(key,out medio);
                Dictionary<string,object> data = null;
                if (json)
                {
                    try { data = Fechas.LeerJson(f.FullName); }
                    catch (Exception e) { Aviso(f.FullName,T("No se pudo leer el JSON: ","Could not read JSON: ")+e.Message); }
                    var duplicado=Regex.Match(key,@"^(.*)(\.[a-z0-9]{2,5})(\(\d+\))$",RegexOptions.IgnoreCase);
                    if (medio==null && duplicado.Success)
                        porNombre.TryGetValue(duplicado.Groups[1].Value+duplicado.Groups[3].Value+duplicado.Groups[2].Value,out medio);
                    object titulo;
                    if (medio==null && !duplicado.Success && data!=null && data.TryGetValue("title",out titulo) && titulo is string)
                    {
                        string nombre=(string)titulo;
                        // Takeout truncates long sidecar names. Require corroborating filename evidence.
                        if (key.Length >= 24 && (nombre.StartsWith(key,StringComparison.OrdinalIgnoreCase) ||
                            key.StartsWith(nombre+".supp",StringComparison.OrdinalIgnoreCase)))
                            porNombre.TryGetValue(nombre,out medio);
                    }
                }
                else if (medio==null && porBase[key].Count()==1) medio=porBase[key].First();
                if (medio==null) { Aviso(f.FullName,T("Adjunto sin pareja inequívoca. Permanece en origen.","No unique match for sidecar. Left in source.")); continue; }
                string suffix = f.Name.StartsWith(medio.Name+".",StringComparison.OrdinalIgnoreCase) ? f.Name.Substring(medio.Name.Length)
                    : json ? ".supplemental-metadata.json" : f.Extension;
                List<Anexo> lista;
                if (!grupos.TryGetValue(medio.Name,out lista)) grupos[medio.Name]=lista=new List<Anexo>();
                lista.Add(new Anexo{Archivo=f,Sufijo=suffix,Google=Fechas.Google(data),PorBase=!json && CI.Equals(key,Path.GetFileNameWithoutExtension(medio.Name))});
            }
            return grupos;
        }
        void Medio(FileInfo f, string[] partes, List<Anexo> anexos)
        {
            bool video=Videos.Contains(f.Extension);
            var anterior=RutaAnterior.Leer(partes,video);
            bool usarCarpeta=anterior!=null && (anterior.Dia.HasValue || anterior.GrupoManual!=null ||
                (anterior.Anio!=null && (cfg.Layout=="year" || (cfg.Layout=="month" && anterior.Mes!=0))));
            Fecha fecha=null;
            if (!usarCarpeta && cfg.Layout!="type") fecha=anexos.Select(a=>a.Google).FirstOrDefault(x=>x!=null);
            if (fecha==null && !usarCarpeta && cfg.Layout!="type")
            {
                try { fecha=Fechas.Archivo(f,video); }
                catch (Exception e) { Aviso(f.FullName,T("Lectura de metadatos: ","Reading metadata: ")+e.Message); }
            }
            if (fecha==null && !usarCarpeta && cfg.Layout!="type") fecha=Fechas.Nombre(Path.GetFileNameWithoutExtension(f.Name));
            DateTime? dia=fecha!=null ? fecha.Dia : anterior!=null ? anterior.Dia : null;
            string year=anterior!=null ? anterior.Anio : partes.Select(p=>Fechas.Anual.Match(p)).Where(m=>m.Success).Select(m=>m.Groups[1].Value).LastOrDefault();
            int mes=dia.HasValue ? dia.Value.Month : anterior!=null ? anterior.Mes : 0;
            if (dia.HasValue) year=dia.Value.ToString("yyyy");
            string fuente=cfg.Layout=="type" ? T("Por tipo","By type") : fecha!=null ? fecha.Fuente :
                anterior!=null && (anterior.Mes!=0 || anterior.Dia.HasValue) ? T("Carpeta de la fototeca","Library folder") :
                year!=null ? T("Solo año de carpeta","Folder year only") : T("Sin fecha","Undated");
            string cat=anterior!=null ? anterior.Categoria : Categoria(f.Name,partes,video);
            string prefix=RutaFecha(cfg.Layout,year,mes,dia);
            if (anterior!=null && anterior.GrupoManual!=null)
            {
                // A user-named year bucket (e.g. a review folder) stays separate.
                prefix=RutaFecha(cfg.Layout,anterior.GrupoManual,mes,dia);
                if (cfg.Layout=="type") prefix=anterior.GrupoManual;
            }
            var albums=anterior!=null ? anterior.Album : partes.Where(p=>!Fechas.Anual.IsMatch(p) && !CarpetaTipo(p));
            string dest=Path.Combine(cfg.Destino,prefix,cat);
            foreach (string parte in albums) dest=Path.Combine(dest,parte);
            if (anexos.Select(a=>a.DestinoDe(f.Name)).Distinct(CI).Count()!=anexos.Count)
            {
                Aviso(f.FullName,T("Varios adjuntos reclaman el mismo nombre. El grupo permanece en origen.","Several sidecars would have the same name. Group left in source."));
                return;
            }
            string target=Path.Combine(dest,f.Name);
            for (int n=2; !Libre(target) || anexos.Any(a=>!Libre(a.DestinoDe(target))); n++)
                target=Path.Combine(dest,Path.GetFileNameWithoutExtension(f.Name)+" ("+n+")"+f.Extension);
            // Read stamps for the entire group before emitting its first row.
            foreach (var file in anexos.Select(a=>a.Archivo).Concat(new[]{f})) { var size=file.Length; }
            Anotar(f,target,cat,fuente);
            foreach (var a in anexos) { Anotar(a.Archivo,a.DestinoDe(target),T("Adjunto","Sidecar"),fuente); adjuntos++; }
            if (!fuentes.ContainsKey(fuente)) fuentes[fuente]=0;
            fuentes[fuente]++;
        }
        public void Recorrer(string root)
        {
            var pendientes=new Stack<string>();
            pendientes.Push(root);
            while (pendientes.Count!=0)
            {
                string dir=pendientes.Pop();
                Console.WriteLine(T("Revisando ","Scanning ")+dir);
                var archivos=new List<FileInfo>();
                try
                {
                    foreach (var entrada in new DirectoryInfo(dir).GetFileSystemInfos().OrderBy(e=>e.Name,CI))
                    {
                        if ((entrada.Attributes & FileAttributes.ReparsePoint)!=0) Aviso(entrada.FullName,T("Enlace omitido.","Link skipped."));
                        else if (entrada is DirectoryInfo) pendientes.Push(entrada.FullName);
                        else archivos.Add((FileInfo)entrada);
                    }
                }
                catch (Exception e) { Aviso(dir,e.Message); continue; }
                var medios=archivos.Where(f=>Fotos.Contains(f.Extension)||Videos.Contains(f.Extension)).ToArray();
                var grupos=Asociar(medios,archivos.Except(medios));
                var partes=Componentes(dir,root);
                string rootName=Path.GetFileName(root);
                if (Fechas.Anual.IsMatch(rootName) || CarpetaTipo(rootName))
                    partes=new[]{rootName}.Concat(partes).ToArray();
                foreach (var f in medios)
                {
                    List<Anexo> lista;
                    if (!grupos.TryGetValue(f.Name,out lista)) lista=new List<Anexo>();
                    try { Medio(f,partes,lista); }
                    catch (PathTooLongException e) { Aviso(f.FullName,e.Message); }
                }
            }
        }
        public string Resumen()
        {
            return string.Format(T("{0:N0} fotos y videos, {1:N0} adjuntos, {2:N0} avisos.","{0:N0} photos and videos, {1:N0} sidecars, {2:N0} warnings."), total-adjuntos,adjuntos,incidencias)
                +Environment.NewLine+string.Join(Environment.NewLine,fuentes.OrderByDescending(p=>p.Value).Select(p=>p.Key+": "+p.Value.ToString("N0")));
        }
        public void Dispose() { plan.Dispose(); avisos.Dispose(); }
    }
    static string Crear(string destino,string[] roots,string layout="day")
    {
        var cfg=new Config{Destino=Full(destino),Origenes=roots.Select(Full).ToArray(),Layout=layout};
        Validar(cfg);
        if (File.Exists(cfg.Destino)) throw new IOException(T("El destino es un archivo.","The destination is a file."));
        foreach (string root in cfg.Origenes) if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        string dir=Path.Combine(Planes,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        Directory.CreateDirectory(dir);
        using (var p=new Planificador(cfg,dir))
        {
            foreach (string root in cfg.Origenes) p.Recorrer(root);
            string resumen=p.Resumen();
            File.WriteAllText(Path.Combine(dir,"Resumen.txt"),resumen,Utf8);
            Console.WriteLine(resumen);
        }
        cfg.SHA256=Hash(Path.Combine(dir,"Plan.csv"));
        File.WriteAllText(Path.Combine(dir,"Lote.json"),Json.Serialize(cfg),Utf8);
        Console.WriteLine(T("Vista previa guardada en ","Preview saved in ")+dir);
        return dir;
    }

    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool MoveFileEx(string existing,string target,uint flags);
    static string Win32Path(string path)
    {
        if (path.StartsWith(@"\\?\")) return path;
        return path.StartsWith(@"\\") ? @"\\?\UNC\"+path.Substring(2) : @"\\?\"+path;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;
        public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInformation info);
    static string Identidad(string file)
    {
        using (var stream=new FileStream(Win32Path(file),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        {
            FileInformation info;
            if (!GetFileInformationByHandle(stream.SafeFileHandle,out info) || (info.IndexHigh==0 && info.IndexLow==0)) return "-";
            return info.Volume.ToString("X8")+info.IndexHigh.ToString("X8")+info.IndexLow.ToString("X8");
        }
    }
    class Estado { public char Paso; public string Id; }
    class Diario : IDisposable
    {
        readonly FileStream stream;
        readonly Dictionary<int,Estado> estados=new Dictionary<int,Estado>();
        public Diario(string dir)
        {
            stream=new FileStream(Path.Combine(dir,"Movimientos.log"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
            // An interrupted append can only leave an incomplete final line.
            if (stream.Length>0)
            {
                stream.Position=stream.Length-1;
                while (stream.ReadByte()!=10)
                {
                    long cut=stream.Position-1;
                    stream.SetLength(cut);
                    if (cut==0) break;
                    stream.Position=cut-1;
                }
            }
            stream.Position=0;
            using (var reader=new StreamReader(stream,Encoding.ASCII,false,4096,true))
            {
                string line;
                while ((line=reader.ReadLine())!=null)
                {
                    string[] cells=line.Split('|');
                    if (cells.Length!=3 || cells[0].Length!=1 || !"IMRO".Contains(cells[0])) throw new IOException(T("Diario de movimientos ilegible.","Unreadable move journal."));
                    estados[int.Parse(cells[1])]=new Estado{Paso=cells[0][0],Id=cells[2]};
                }
            }
            stream.Position=stream.Length;
        }
        public Estado Leer(int i) { Estado e; return estados.TryGetValue(i,out e) ? e : new Estado{Paso='O',Id="-"}; }
        public void Marcar(int i,char paso,string id)
        {
            byte[] b=Encoding.ASCII.GetBytes(paso+"|"+i+"|"+id+"\n");
            try { stream.Write(b,0,b.Length); stream.Flush(true); }
            catch (Exception ex) { throw new FalloDiario(ex); }
            estados[i]=new Estado{Paso=paso,Id=id};
        }
        public void Dispose() { stream.Dispose(); }
    }
    static void Sello(string file, string[] row)
    {
        var f=new FileInfo(Win32Path(file));
        if (!f.Exists || f.Length!=long.Parse(row[4]) || f.LastWriteTimeUtc.Ticks!=long.Parse(row[5]))
            throw new IOException(T("Falta el archivo o cambiaron su tamaño o fecha de modificación.","File missing, or its size or modification time changed."));
    }
    static bool Existe(string path)
    {
        try { File.GetAttributes(Win32Path(path)); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
    static Estado Conciliar(Diario d,int index,Estado e,string[] row)
    {
        bool ida=e.Paso=='I';
        int inicio=ida ? 0 : 1;
        bool enInicio=Existe(row[inicio]), enFin=Existe(row[1-inicio]);
        if (enInicio==enFin || e.Id=="-") throw new IOException(T("Movimiento interrumpido. Verifica ambas rutas antes de reintentar.","Interrupted move. Check both paths before retrying."));
        string actual=enInicio ? row[inicio] : row[1-inicio];
        Sello(actual,row);
        if (Identidad(actual)!=e.Id) throw new IOException(T("El archivo no coincide con el movimiento interrumpido.","The file does not match the interrupted move."));
        d.Marcar(index,(ida!=enInicio) ? 'M' : 'O',e.Id);
        return d.Leer(index);
    }
    static bool MoverFila(Diario d,int index,string[] row,bool deshacer)
    {
        Estado e=d.Leer(index);
        if ((deshacer && e.Paso=='O') || (!deshacer && e.Paso=='M')) return false;
        SinEnlaces(row[0]);
        SinEnlaces(row[1]);
        if (e.Paso=='I' || e.Paso=='R') e=Conciliar(d,index,e,row);
        if ((deshacer && e.Paso=='O') || (!deshacer && e.Paso=='M')) return false;
        string from=row[deshacer ? 1 : 0], to=row[deshacer ? 0 : 1];
        Sello(from,row);
        if (Existe(to)) throw new IOException(T("El destino ya está ocupado.","The destination already exists."));
        string id=Identidad(from);
        if (deshacer && e.Id!="-" && e.Id!=id) throw new IOException(T("Cambió la identidad del archivo.","The file identity changed."));
        Directory.CreateDirectory(Win32Path(Path.GetDirectoryName(to)));
        d.Marcar(index,deshacer ? 'R' : 'I',id);
        // No REPLACE_EXISTING or COPY_ALLOWED. Across volumes Windows must fail.
        if (!MoveFileEx(Win32Path(from),Win32Path(to),0))
        {
            int err=Marshal.GetLastWin32Error();
            // A lost SMB reply does not prove the rename failed. Reconcile on retry.
            throw new Win32Exception(err);
        }
        d.Marcar(index,deshacer ? 'O' : 'M',id);
        return true;
    }
    static int Ejecutar(string dir,bool deshacer)
    {
        dir=Full(dir);
        string archivo=Path.Combine(dir,"Plan.csv");
        var cfg=Json.Deserialize<Config>(File.ReadAllText(Path.Combine(dir,"Lote.json")));
        Validar(cfg);
        int index=0, fallos=0, hechos=0;
        string log=Path.Combine(dir,(deshacer?"Deshacer-":"Aplicar-")+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".csv");
        using (var lectura=File.Open(archivo,FileMode.Open,FileAccess.Read,FileShare.Read))
        using (var csv=new TextFieldParser(lectura,Encoding.UTF8,true,true))
        using (var diario=new Diario(dir))
        {
            if (Hash(archivo)!=cfg.SHA256) throw new IOException(T("Cambió Plan.csv. Genera una vista previa nueva.","Plan.csv changed. Create a new preview."));
            csv.SetDelimiters(",");
            csv.TrimWhiteSpace=false;
            csv.ReadFields();
            using (var errores=new StreamWriter(log,false,Utf8))
            {
                Csv(errores,T("Origen","Source"),T("Destino","Destination"),T("Motivo","Reason"));
                bool medioPendiente=false;
                while (!csv.EndOfData)
                {
                    var row=csv.ReadFields();
                    index++;
                    if (row.Length!=6 || !cfg.Origenes.Any(root=>Dentro(Full(row[0]),root)) || !Dentro(Full(row[1]),cfg.Destino))
                        throw new IOException(T("Ruta fuera del lote.","Path outside this batch."));
                    bool adjunto=row[2]=="Adjunto" || row[2]=="Sidecar";
                    if (!adjunto) medioPendiente=false;
                    try
                    {
                        if (adjunto && medioPendiente) throw new IOException(T("El archivo principal está pendiente.","The associated media file is pending."));
                        if (MoverFila(diario,index,row,deshacer)) hechos++;
                    }
                    catch (Exception e)
                    {
                        // A journal write failure must stop the run before another rename.
                        if (e is FalloDiario) throw;
                        if (!adjunto) medioPendiente=true;
                        Csv(errores,row[0],row[1],e.Message);
                        fallos++;
                    }
                    if (index%250==0) Console.WriteLine(T("{0:N0} revisados, {1:N0} movidos, {2:N0} pendientes.","{0:N0} checked, {1:N0} moved, {2:N0} pending."),index,hechos,fallos);
                }
            }
        }
        Console.WriteLine(T("{0:N0} archivos {1}. {2:N0} pendientes.","{0:N0} files {1}. {2:N0} pending."),hechos,deshacer?T("devueltos","restored"):T("movidos","moved"),fallos);
        if (fallos>0) Console.WriteLine(T("Consulta ","See ")+log);
        return fallos==0 ? 0 : 2;
    }
    class FalloDiario : IOException { public FalloDiario(Exception inner):base(T("No se pudo guardar el diario. Operación detenida.","Could not save the journal. Operation stopped."),inner) {} }
    static string Elegir(bool reciente, bool conMovimientos)
    {
        string[] dirs=Directory.Exists(Planes) ? Directory.GetDirectories(Planes).Where(p=>File.Exists(Path.Combine(p,"Lote.json"))).OrderByDescending(p=>p,CI).ToArray() : new string[0];
        if (conMovimientos) dirs=dirs.Where(p=>File.Exists(Path.Combine(p,"Movimientos.log"))).ToArray();
        if (dirs.Length==0)
        {
            Console.WriteLine(conMovimientos ? T("Aún no hay movimientos que deshacer.","No moves to undo yet.") : T("Primero elige 1 para preparar una vista previa.","Choose 1 to prepare a preview first."));
            return null;
        }
        if (reciente || dirs.Length==1) return dirs[0];
        for (int i=0;i<dirs.Length;i++) Console.WriteLine("{0}. {1}",i+1,Path.GetFileName(dirs[i]));
        Console.Write(T("Número de lote (Enter = más reciente): ","Batch number (Enter = latest): "));
        string value=Console.ReadLine();
        int n;
        if (string.IsNullOrWhiteSpace(value)) return dirs[0];
        if (!int.TryParse(value,out n) || n<1 || n>dirs.Length) throw new IOException(T("Número de lote inválido.","Invalid batch number."));
        return dirs[n-1];
    }
}
