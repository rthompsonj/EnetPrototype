using System;
using System.IO;
using System.Reflection;
using System.Text;
using SoL.Networking.Replication;
using UnityEditor;

public static class Generator
{
    private static string kNamespace = "SoL.Networking.Replication";
    private static string kGeneratedBasePath = "Assets/Scripts/Replication/Generated";
    private static string kGeneratedSuffix = "generated";
    
    [MenuItem("SoL/Generate All")]
    public static void GenerateAll()
    {
        bool update = false;
        Type replicationInterface = typeof(IReplicationLayer);
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type thisType in asm.GetTypes())
            {
                if (replicationInterface.IsAssignableFrom(thisType))
                {
                    update = update || GenerateSyncCode(thisType);
                }
            }
        }

        if (update)
        {
            AssetDatabase.Refresh();   
        }
    }

    [MenuItem("SoL/Delete All Generated")]
    public static void DeleteAll()
    {
        int deleted = 0;
        var files = Directory.GetFiles(kGeneratedBasePath);
        for (int i = 0; i < files.Length; i++)
        {
            if(files[i].Contains(kGeneratedSuffix))
            {
                File.Delete(files[i]);
                deleted += 1;
            }
        }

        if (deleted > 0)
        {
            AssetDatabase.Refresh();   
        }
    }
    
    private static bool GenerateSyncCode(Type t)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));

        var writer = new Writer
        {
            buffer = new StringBuilder()
        };

        int nSyncFields = 0;
        
        writer.WriteLine($"namespace {kNamespace}");
        writer.BeginBlock();
        writer.WriteLine($"public partial class {t.Name}");
        writer.BeginBlock();
        
        //writer.WriteLine("protected sealed override void RegisterSyncs()");
        writer.WriteLine("protected override int RegisterSyncs()");
        writer.BeginBlock();
        writer.WriteLine("var cnt = base.RegisterSyncs();");
        for (int i = 0; i < fields.Length; i++)
        {
            if (typeof(ISynchronizedVariable).IsAssignableFrom(fields[i].FieldType))
            {
                writer.WriteLine($"m_syncs.Add({fields[i].Name});");
                writer.WriteLine($"{fields[i].Name}.BitFlag = 1 << cnt;");
                writer.WriteLine("cnt += 1;");
                nSyncFields += 1;
            }
        }        
        writer.WriteLine("return cnt;");
        writer.EndBlock();
        writer.EndBlock();
        writer.EndBlock();
        
        var code = writer.buffer.ToString();
        var fileName = $"{t.Name}_{kGeneratedSuffix}.cs";
        var path = $"{kGeneratedBasePath}/{fileName}";
        if (File.Exists(path))
        {
            var existingCode = File.ReadAllText(path);
            if (existingCode == code)
            {
                return false;   
            }
            if (nSyncFields == 0)
            {
                File.Delete(path);
            }
        }

        if (nSyncFields == 0)
            return false;
        
        File.WriteAllText(path, code);
        return true;
    }
    
    #region HELPERS
    
    private const int kSpacesPerIndentLevel = 4;
    
    private struct Writer
    {
        public StringBuilder buffer;
        public int indentLevel;

        public void BeginBlock()
        {
            WriteIndent();
            buffer.Append("{\n");
            ++indentLevel;
        }

        public void EndBlock()
        {
            --indentLevel;
            WriteIndent();
            buffer.Append("}\n");
        }

        public void WriteLine(string text)
        {
            WriteIndent();
            buffer.Append(text);
            buffer.Append('\n');
        }

        private void WriteIndent()
        {
            for (var i = 0; i < indentLevel; ++i)
            {
                for (var n = 0; n < kSpacesPerIndentLevel; ++n)
                    buffer.Append(' ');
            }
        }
    }
    
    #endregion
}
