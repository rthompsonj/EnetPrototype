using System;
using System.IO;
using System.Reflection;
using System.Text;
using NextSimple;
using Threaded;
using UnityEditor;

public static class Generator
{
    [MenuItem("SoL/Generate All")]
    public static void GenerateAll()
    {
        GenerateSyncCode<BaseEntity>();
        GenerateSyncCode<InheritedBaseEntity>();
        AssetDatabase.Refresh();
    }
    
    public static void GenerateSyncCode<T>()
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));

        var writer = new Writer
        {
            buffer = new StringBuilder()
        };
        
        writer.WriteLine("namespace NextSimple");
        writer.BeginBlock();
        writer.WriteLine($"public partial class {typeof(T).Name}");
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
            }
        }        
        writer.WriteLine("return cnt;");
        writer.EndBlock();
        writer.EndBlock();
        writer.EndBlock();
        
        var code = writer.buffer.ToString();
        var fileName = $"{typeof(T).Name}_generated.cs";
        var path = $"Assets/Scripts/Generated/{fileName}";
        if (File.Exists(path))
        {
            var existingCode = File.ReadAllText(path);
            if (existingCode == code)
                return;
        }
        
        File.WriteAllText(path, code);
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
