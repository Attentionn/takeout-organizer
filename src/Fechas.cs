using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal class Fecha
{
    public DateTime Dia;
    public string Fuente;
    public Fecha(DateTime dia, string fuente) { Dia = dia.Date; Fuente = fuente; }
}

internal static class Fechas
{
    public static readonly Regex Anual = new Regex(@"^(?:photos from |fotos(?: de)? )?((?:19|20)\d{2})$", RegexOptions.IgnoreCase);
    static readonly Regex EnNombre = new Regex(@"(?<!\d)((?:19|20)\d{2})[-_.]?([01]\d)[-_.]?([0-3]\d)(?=$|[^\d]|\d{6}(?:\d{3})?(?:$|\D))");
    static readonly Regex FbTiempo = new Regex(@"^FB_IMG_(\d{10}|\d{13})(?:$|[^\d])", RegexOptions.IgnoreCase);
    static readonly DateTime Unix = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    public static Dictionary<string,object> LeerJson(string path)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024) return null;
        return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
    }
    static Fecha Valida(DateTime dia, string fuente)
    {
        return dia.Year >= 1800 && dia.Date <= DateTime.Today.AddDays(2) ? new Fecha(dia, fuente) : null;
    }
    public static Fecha Google(Dictionary<string,object> datos)
    {
        object valor;
        if (datos == null || !datos.TryGetValue("photoTakenTime", out valor)) return null;
        var tiempo = valor as Dictionary<string,object>;
        long segundos;
        if (tiempo == null || !tiempo.TryGetValue("timestamp", out valor) || !long.TryParse(Convert.ToString(valor), out segundos)) return null;
        if (segundos < -5364662400L || segundos > 253402214399L) return null;
        return Valida(Unix.AddSeconds(segundos).ToLocalTime(), Idioma.Texto("Google Fotos","Google Photos"));
    }
    public static Fecha Nombre(string nombre)
    {
        var fb = FbTiempo.Match(nombre);
        if (fb.Success)
        {
            string s = fb.Groups[1].Value;
            return Valida(Unix.AddSeconds(long.Parse(s.Substring(0, 10))).ToLocalTime(), Idioma.Texto("Nombre Facebook","Facebook filename"));
        }
        foreach (Match m in EnNombre.Matches(nombre))
        {
            DateTime dia;
            if (DateTime.TryParseExact(m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value,
                "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dia))
                return Valida(dia, Idioma.Texto("Nombre","Filename"));
        }
        return null;
    }
    static uint U32(byte[] b, int pos, bool little)
    {
        if (little) return (uint)(b[pos] | b[pos+1] << 8 | b[pos+2] << 16 | b[pos+3] << 24);
        return (uint)(b[pos] << 24 | b[pos+1] << 16 | b[pos+2] << 8 | b[pos+3]);
    }
    static int U16(byte[] b, int pos, bool little)
    {
        return little ? b[pos] | b[pos+1] << 8 : b[pos] << 8 | b[pos+1];
    }
    static Fecha Exif(byte[] b)
    {
        const int start = 6;
        if (b.Length < 14 || Encoding.ASCII.GetString(b, 0, 6) != "Exif\0\0") return null;
        bool little = b[start] == 'I' && b[start+1] == 'I';
        if (!little && (b[start] != 'M' || b[start+1] != 'M')) return null;
        if (U16(b, start+2, little) != 42) return null;
        uint offset = U32(b, start+4, little);
        for (int level = 0; level < 2 && offset > 0 && offset <= b.Length-start-2; level++)
        {
            int pos = start + (int)offset;
            int count = U16(b, pos, little);
            offset = 0;
            Fecha digital = null;
            for (int i = 0, at = pos+2; i < count && at <= b.Length-12; i++, at += 12)
            {
                int tag = U16(b, at, little);
                uint value = U32(b, at+8, little);
                if (tag == 0x8769 && level == 0) offset = value;
                if ((tag != 0x9003 && tag != 0x9004) || U16(b, at+2, little) != 2 || value > b.Length-start-19) continue;
                DateTime dia;
                if (!DateTime.TryParseExact(Encoding.ASCII.GetString(b, start+(int)value, 19), "yyyy:MM:dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dia)) continue;
                var fecha = Valida(dia, tag == 0x9003 ? Idioma.Texto("EXIF original","Original EXIF date") : Idioma.Texto("EXIF digitalizacion","Digitized EXIF date"));
                if (tag == 0x9003 && fecha != null) return fecha;
                digital = fecha;
            }
            if (digital != null) return digital;
        }
        return null;
    }
    static Fecha Jpeg(BinaryReader r)
    {
        if (r.BaseStream.Length < 4 || r.ReadByte() != 255 || r.ReadByte() != 216) return null;
        while (r.BaseStream.Position+4 <= r.BaseStream.Length && r.ReadByte() == 255)
        {
            int marker;
            do { marker = r.ReadByte(); } while (marker == 255);
            if (marker == 218 || marker == 217) break;
            int len = (r.ReadByte() << 8 | r.ReadByte()) - 2;
            if (len < 0 || len > r.BaseStream.Length-r.BaseStream.Position) break;
            if (marker != 225) r.BaseStream.Seek(len, SeekOrigin.Current);
            else
            {
                var fecha = Exif(r.ReadBytes(len));
                if (fecha != null) return fecha;
            }
        }
        return null;
    }
    static ulong Big64(byte[] b, int pos)
    {
        return ((ulong)U32(b, pos, false) << 32) | U32(b, pos+4, false);
    }
    static Fecha QuickTime(BinaryReader r, long end, bool inMoov)
    {
        var stream = r.BaseStream;
        while (end-stream.Position >= 8)
        {
            long start = stream.Position;
            byte[] header = r.ReadBytes(8);
            long size = U32(header, 0, false);
            string name = Encoding.ASCII.GetString(header, 4, 4);
            if (size == 1 && end-stream.Position >= 8) size = unchecked((long)Big64(r.ReadBytes(8), 0));
            else if (size == 0) size = end-start;
            if (size < stream.Position-start || size > end-start) break;
            long next = start+size;
            if (!inMoov && name == "moov") return QuickTime(r, next, true);
            if (inMoov && name == "mvhd" && next-stream.Position >= 12)
            {
                byte[] b = r.ReadBytes(12);
                if (b[0] > 1) return null;
                ulong sec = b[0] == 1 ? Big64(b, 4) : U32(b, 4, false);
                if (sec == 0 || sec > 10000000000UL) return null;
                return Valida(new DateTime(1904,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(sec).ToLocalTime(), Idioma.Texto("Creacion del video","Video creation date"));
            }
            stream.Position = next;
        }
        return null;
    }
    // Windows exposes capture dates for additional formats through installed file handlers.
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid fmtid; public uint pid; }
    [StructLayout(LayoutKind.Explicit, Size=24)] struct Variant
    {
        [FieldOffset(0)] public ushort type;
        [FieldOffset(8)] public long fileTime;
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint n);
        [PreserveSig] int GetAt(uint i, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out Variant value);
    }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr context, uint flags, ref Guid iid, out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
    static Fecha Windows(string path, bool video)
    {
        Guid iid = typeof(IPropertyStore).GUID;
        IPropertyStore store;
        if (SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref iid, out store) < 0) return null;
        try
        {
            var key = video ? new PropertyKey {fmtid=new Guid("2E4B640D-5019-46D8-8881-55414CC5CAA0"),pid=100}
                            : new PropertyKey {fmtid=new Guid("14B81DA1-0135-4D31-96D9-6CBFC9671A99"),pid=36867};
            Variant v;
            int hr = store.GetValue(ref key, out v);
            try
            {
                if (hr < 0 || v.type != 64 || v.fileTime <= 0) return null;
                DateTime utc = DateTime.FromFileTimeUtc(v.fileTime);
                return Valida(utc.ToLocalTime(), video ? Idioma.Texto("Codificacion del video","Video encoding date") : Idioma.Texto("Fecha de toma Windows","Windows date taken"));
            }
            finally { PropVariantClear(ref v); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }
    public static Fecha Archivo(FileInfo file, bool video)
    {
        string ext = file.Extension.ToLowerInvariant();
        bool jpeg = ext == ".jpg" || ext == ".jpeg" || ext == ".jpe";
        bool qt = ext == ".mp4" || ext == ".mov" || ext == ".m4v" || ext == ".3gp" || ext == ".3g2";
        if (jpeg || qt)
        {
            using (var r = new BinaryReader(File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
                return jpeg ? Jpeg(r) : QuickTime(r, r.BaseStream.Length, false);
        }
        return Windows(file.FullName, video);
    }
}
