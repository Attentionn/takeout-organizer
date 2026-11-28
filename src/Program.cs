using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal static class Idioma
{
    public static CultureInfo Cultura { get; private set; }
    public static void Usar(string codigo)
    {
        Cultura = CultureInfo.GetCultureInfo(codigo == "es" ? "es-MX" : "en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = Cultura;
        System.Threading.Thread.CurrentThread.CurrentUICulture = Cultura;
    }
    public static string Texto(string es, string en)
    {
        return Cultura.TwoLetterISOLanguageName == "es" ? es : en;
    }
}

internal static partial class Organizador
{
    static void ElegirIdioma()
    {
        Console.WriteLine("1. English\n2. Español");
        while (true)
        {
            Console.Write("Language / Idioma [" + Idioma.Cultura.NativeName + "]: ");
            string value = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (value == "") return;
            if (value == "1" || value == "en") { Idioma.Usar("en"); return; }
            if (value == "2" || value == "es") { Idioma.Usar("es"); return; }
            Console.WriteLine("1 / 2");
        }
    }
    static string LeerRuta()
    {
        string line = Console.ReadLine();
        return line == null ? null : line.Trim().Trim('"');
    }
    static void Preparar()
    {
        var sources = new List<string>();
        Console.WriteLine(T("Escribe o arrastra una carpeta de origen por línea. Presiona Enter cuando termines.",
                            "Type or drag one source folder per line. Press Enter when finished."));
        while (true)
        {
            Console.Write(T("Origen","Source")+" "+(sources.Count+1)+"> ");
            string input = LeerRuta();
            if (string.IsNullOrEmpty(input)) break;
            if (!Directory.Exists(input))
            {
                Console.WriteLine(T("No se encontró la carpeta.","Folder not found."));
                continue;
            }
            sources.Add(Full(input));
        }
        if (sources.Count == 0) return;
        var parent = Directory.GetParent(sources[0]);
        if (parent == null)
            throw new IOException(T("Elige carpetas dentro del disco como origen.","Select folders inside the drive as sources."));
        Console.Write(T("Guardar la fototeca dentro de","Create the library inside")+" ["+parent.FullName+"]: ");
        string folder = LeerRuta();
        if (folder == null) return;
        if (folder == "") folder = parent.FullName;
        string defaultName = T("Fototeca organizada","Photo Library");
        Console.Write(T("Nombre de tu fototeca","Library name")+" ["+defaultName+"]: ");
        string name = Console.ReadLine();
        if (name == null) return;
        if (name == "") name = defaultName;
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.EndsWith(".") || name.EndsWith(" ") ||
            Regex.IsMatch(name,@"^(?:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³]|CONIN\$|CONOUT\$)(?:\.|$)",RegexOptions.IgnoreCase))
            throw new IOException(T("Escribe un nombre de carpeta válido.","Enter a valid folder name."));
        Console.WriteLine(T("¿Cómo quieres agrupar los archivos?\n1. Año / mes / día / tipo\n2. Año / mes / tipo\n3. Año / tipo (juntar los meses)\n4. Solo por tipo (juntar todos los años)",
                            "How should files be grouped?\n1. Year / month / day / type\n2. Year / month / type\n3. Year / type (combine months)\n4. Type only (combine years)"));
        string[] layouts={"day","month","year","type"};
        while (true)
        {
            Console.Write("[1]> ");
            string choice=Console.ReadLine();
            if (choice==null) return;
            int index;
            if (choice=="") choice="1";
            if (!int.TryParse(choice,out index) || index<1 || index>4) continue;
            Crear(Path.Combine(folder,name),sources.ToArray(),layouts[index-1]);
            return;
        }
    }
    static void Menu()
    {
        while (true)
        {
            Console.WriteLine(T("\nOrganizer\n1. Preparar una vista previa\n2. Organizar con la última vista previa\n3. Deshacer un movimiento\n4. Salir\n5. Elegir otra vista previa\n6. Cambiar idioma",
                                "\nOrganizer\n1. Create a preview\n2. Organize using the latest preview\n3. Undo a move\n4. Exit\n5. Select another preview\n6. Change language"));
            Console.Write("> ");
            string opt = Console.ReadLine();
            if (opt == null || opt == "4") return;
            try
            {
                if (opt == "1") { Preparar(); continue; }
                if (opt == "6") { ElegirIdioma(); continue; }
                if (opt != "2" && opt != "3" && opt != "5") continue;
                string dir = Elegir(opt=="2",opt=="3");
                if (dir == null) continue;
                var cfg = Json.Deserialize<Config>(File.ReadAllText(Path.Combine(dir,"Lote.json")));
                Console.WriteLine(File.ReadAllText(Path.Combine(dir,"Resumen.txt")));
                Console.WriteLine(T("Orígenes: ","Sources: ")+string.Join(", ",cfg.Origenes));
                Console.WriteLine(T("Destino: ","Destination: ")+cfg.Destino);
                Console.WriteLine("Plan: "+Path.Combine(dir,"Plan.csv"));
                bool undo = opt == "3";
                string word = undo ? T("DESHACER","UNDO") : T("MOVER","MOVE");
                Console.Write(T("Escribe {0} para continuar o presiona Enter para volver: ",
                                "Type {0} to continue or press Enter to go back: "),word);
                if (CI.Equals((Console.ReadLine() ?? "").Trim(),word)) Ejecutar(dir,undo);
            }
            catch (Exception e) { Console.Error.WriteLine(e.Message); }
        }
    }
    static void Uso()
    {
        Console.WriteLine(T("Organizer [--lang es|en] prever [--layout day|month|year|type] DESTINO ORIGEN [ORIGEN ...]\nOrganizer aplicar CARPETA_DEL_LOTE\nOrganizer deshacer CARPETA_DEL_LOTE",
                            "Organizer [--lang en|es] preview [--layout day|month|year|type] DESTINATION SOURCE [SOURCE ...]\nOrganizer apply BATCH_FOLDER\nOrganizer undo BATCH_FOLDER"));
    }
    [STAThread] public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        bool explicitLanguage = args.Length > 0 && CI.Equals(args[0],"--lang");
        Idioma.Usar(systemLanguage);
        try
        {
            if (explicitLanguage)
            {
                if (args.Length < 2 || !(CI.Equals(args[1],"es") || CI.Equals(args[1],"en"))) { Uso(); return 1; }
                Idioma.Usar(args[1].ToLowerInvariant());
                args = args.Skip(2).ToArray();
            }
            if (args.Length == 0)
            {
                if (!explicitLanguage) ElegirIdioma();
                Menu();
                return 0;
            }
            string command = args[0].ToLowerInvariant();
            if (!explicitLanguage && new[]{"prever","aplicar","deshacer"}.Contains(command)) Idioma.Usar("es");
            if (!explicitLanguage && new[]{"preview","apply","undo"}.Contains(command)) Idioma.Usar("en");
            if (command == "--help" || command == "-h") { Uso(); return 0; }
            if (command == "prever" || command == "preview")
            {
                if (args.Length == 1) Preparar();
                else if (CI.Equals(args[1],"--layout"))
                {
                    if (args.Length<5 || !new[]{"day","month","year","type"}.Contains(args[2])) { Uso(); return 1; }
                    Crear(args[3],args.Skip(4).ToArray(),args[2]);
                }
                else if (args.Length >= 3) Crear(args[1],args.Skip(2).ToArray());
                else { Uso(); return 1; }
                return 0;
            }
            if (args.Length == 2)
            {
                if (command == "apply" || command == "aplicar") return Ejecutar(args[1],false);
                if (command == "undo" || command == "deshacer") return Ejecutar(args[1],true);
            }
            Uso();
            return 1;
        }
        catch (Exception e) { Console.Error.WriteLine(T("Se detuvo la operación: ","Operation stopped: ")+e.Message); return 1; }
    }
}
