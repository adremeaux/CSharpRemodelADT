using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/*
# CSharpRemodelADT

Code generator for creating C# Algebraic Data Type implementations from a template

Based on Facebook's Remodel tool: https://github.com/facebook/remodel

#### Build
Compile for .NET 7+ (https://github.com/dotnet/roslyn):  
`csc remodel.cs`

#### Run  
`./remodel <dir>`

When run with `./remodel <dir>`, will recursively search for all .adtValue files in the
directories and generate corresponding <class_name>.cs files in the same directory

#### adtValue File Format
```
#Commented Line. No trailing comments allowed.
<Set arguments>
<classname> {
  <baseLevelVarType> <name>
  %<baseLevelVarWithDefaultType> <name> 
  #<fixedLineInsertion>
  <fieldname1> {
    <fieldVarType> <name>
  }
  <fieldname2> {
    <fieldVarType> <name>
  }
}
```

#### Modifiers:
* Preceding a base level variable with `%` will generate it with a default value in the constructor:
```
//SomeField.adtValue
//int i
//%SomeClass sc

public sealed class SomeField {
  public SomeField(int i, SomeClass sc = default(SomeClass)) {
  ...
```

* Preceding a base level line with a `#` will cause that lined to be inserted into the base class
as-is with no modification. You can supply numerous lines. You usually shouldn't use this as it breaks the
standard for the pattern.

* `Set` Arguments:
At the start of the file, type `Set <some_arg>` to enable specific features:

  * `Set AllowNulls`
This will create null defaults in the match functions so you don't have to match the whole thing
  * `Set KnownTypes`
This will generate `[KnownType...]` directives at the top for json serialization with Newtonsoft

*/

class Remodel {

  public struct ADT {
    public string name;
    public bool allowNulls;
    public bool knownTypes;
    public List<StrPair> vars;
    public List<StrPair> varsWithDefaults; //lines preceded by '%' will be inserted with optional 
    public List<string> fixedLines; //lines preceded by '&' will be inserted directly into the top-level class
    public List<ADTImpl> impls;

    public ADT(string x) {
      name = "";
      vars = new List<StrPair>();
      varsWithDefaults = new List<StrPair>();
      fixedLines = new List<string>();
      impls = new List<ADTImpl>();
      allowNulls = false;
      knownTypes = false;
    }

    public override string ToString() => "ADT: " + name + " | " + impls.ToLog();
  }

  public struct ADTImpl {
    public string name;
    public List<StrPair> vars;

    public ADTImpl(string x) {
      name = "";
      vars = new List<StrPair>();
    }

    public override string ToString() {
      return "ADTImpl: " + name + " | " + vars.ToLog();
    }
  }

  public struct StrPair {
    public string s1;
    public string s2;
    public string str => s1 + " " + s2;
    public StrPair(string s1, string s2) {
      this.s1 = s1; this.s2 = s2;
    }

    public override string ToString() {
      return "'" + s1 + " " + s2 + "'";
    }
  }

  static void Main(string[] args) {
    string basePath = AppDomain.CurrentDomain.BaseDirectory;
    if (args.Length >= 1) basePath = Path.Combine(basePath, args[0]);
    Console.WriteLine("Searching for files in " + basePath);

    string[] files = Directory.GetFiles(basePath, "*.adtValue", SearchOption.AllDirectories);
    files.ToList().ForEach(s => Console.WriteLine("  " + s));
    files.ToList().ForEach(s => GenerateFile(s));
  }

  static void GenerateFile(string filePath) {
    // Read the text file line by line
    Console.WriteLine("");

    string[] lines = File.ReadAllLines(filePath);
    int bracketCount = 0;
    ADT mainADT = new ADT("");
    ADTImpl currentImpl = new ADTImpl("");
    foreach (string line in lines) {
      if (line.Trim()[0].ToString() == "#") continue;
      if (line.Trim().Length == 0) continue;
      string[] words = line.Trim().Split(' ');

      if (words.Last() == "{") {
        if (bracketCount == 0) {
          mainADT.name = words.First();
        } else if (bracketCount == 1) {
          currentImpl = new ADTImpl("");
          currentImpl.name = words.First();
        } else {
          Console.WriteLine("[REMODEL] Error: too many { brackets");
          return;
        }

        bracketCount++;
      } else if (words.Last() == "}") {
        if (bracketCount == 2) {
          mainADT.impls.Add(currentImpl);
        } else if (bracketCount == 1) {
          Console.WriteLine("Parsing completed for " + mainADT.name);
        } else {
          Console.WriteLine("[REMODEL] Error: too many } brackets");
          return;
        }

        bracketCount--;
      } else {
        if (bracketCount == 0) {
          if (words.Count() == 2 && words[0] == "Set") {
            if (words[1] == "AllowNulls") {
              mainADT.allowNulls = true;
            } else if (words[1] == "KnownTypes") {
              mainADT.knownTypes = true;
            } else {
              Console.WriteLine("[REMODEL] Error: Unknown command: " + line.Trim());
            }
            continue;
          } else {
            Console.WriteLine("[REMODEL] Error: Unknown command: " + line.Trim());
            continue;
          }
        }
        if (bracketCount > 2) {
          Console.WriteLine("[REMODEL] Error: There is a line without brackets in the wrong place");
          return;
        }
        if (bracketCount == 1) {
          if (words[0][0] == '&') {
            mainADT.fixedLines.Add(line.Trim().Substring(1));
          } else if (words[0][0] == '%') {
            mainADT.varsWithDefaults.Add(new StrPair(words[0].Substring(1), words[1]));
          } else {
            if (words.Length != 2) {
              Console.WriteLine("[REMODEL] Error: There should be exactly two words on the line: " + words.ToLog());
              return;
            }
            mainADT.vars.Add(new StrPair(words[0], words[1]));
          }
        } else {
          if (words.Length != 2) {
            Console.WriteLine("[REMODEL] Error: There should be exactly two words on the line: " + words.ToLog());
            return;
          }
          currentImpl.vars.Add(new StrPair(words[0], words[1]));
        }
      }
    }

    string output = GetOutput(mainADT);
    string outputPath = Path.Combine(Path.GetDirectoryName(filePath), mainADT.name + ".cs");
    File.WriteAllText(outputPath, output);
    Console.WriteLine("ADT model generated successfully for " + mainADT.name);
  }

  public static string GetOutput(ADT adt) {
    string ser = "[Serializable]";
    string nullStr = adt.allowNulls ? " = null" : "";
    string nl = "\n";
    string tab = "  ";
    string s = "";
    int indent = 0;
    string Indent() {
      string s2 = "";
      for (int i = 0; i < indent; i++) s2 += tab;
      return s2;
    }

    s += "using System;" + nl;
    s += "using System.Runtime.Serialization;" + nl;
    s += nl;

    if (adt.knownTypes) {
      foreach (ADTImpl impl in adt.impls) {
        s += "[KnownType(typeof(" + impl.name + "))]" + nl;
      }
    }

    s += ser + nl;
    s += "public abstract class " + adt.name + " {" + nl;
    indent++;

    //Constructor
    s += Indent() + "private " + adt.name + "() { }" + nl + nl;

    //ToString()
    s += Indent() + "public override string ToString() => this.PrettyPrint();" + nl + nl;

    //vars declaration
    foreach (StrPair strPair in adt.vars.Concat(adt.varsWithDefaults))
      s += Indent() + "public " + strPair.str + ";" + nl;
    s += nl;

    //bullshit insertion
    if (adt.fixedLines.Count > 0) {
      s += Indent() + "//fixed area" + nl;
      foreach (string bs in adt.fixedLines)
        s += Indent() + bs + nl;
      s += Indent() + "//end fixed area" + nl + nl;
    }

    //Subclasses
    foreach (ADTImpl impl in adt.impls) {
      s += Indent() + ser + nl;
      s += Indent() + "public sealed class " + impl.name + " : " + adt.name + " {" + nl;

      //Subclass declaration
      indent++;
      foreach (StrPair strPair in impl.vars) {
        s += Indent() + "public " + strPair.str + ";" + nl;
      }

      //Subclass constructor
      string vars = String.Join(", ",
        adt.vars.Concat(impl.vars).Select(sp => sp.str)
        .Concat(adt.varsWithDefaults.Select(sp => sp.str + " = default(" + sp.s1 + ")"))
      );

      s += Indent() + "public " + impl.name + "(" + vars + ") {" + nl;

      //this.var = var...
      indent++;
      foreach (StrPair strPair in adt.vars.Concat(adt.varsWithDefaults).Concat(impl.vars)) {
        s += Indent() + "this." + strPair.s2 + " = " + strPair.s2 + ";" + nl;
      }
      indent--;

      s += Indent() + "}" + nl;
      indent--;

      s += Indent() + "}" + nl + nl;
    }

    //T Match<T>()
    {
      //header
      s += Indent() + "public T Match<T>(" + nl;
      indent += 2;
      foreach (ADTImpl impl in adt.impls) {
        string comma = impl.Equals(adt.impls.Last()) ? "" : ",";
        s += Indent() + "Func<" + impl.name + ", T> " + impl.name + nullStr + comma + nl;
      }
      indent -= 2;
      s += Indent() + ") {" + nl;

      //body
      indent++;
      for (int i = 0; i < adt.impls.Count; i++) {
        string iName = adt.impls[i].name;
        s += Indent();
        if (i != 0) s += "} else ";
        s += "if (this is " + iName + " s" + i + ") {" + nl;
        indent++;
        s += Indent() + "return " + iName + " != null ? " + iName + "(s" + i + ") : default(T);" + nl;
        indent--;
      }
      s += Indent() + "}" + nl;
      s += Indent() + "return default(T);" + nl;
      indent--;
      s += Indent() + "}" + nl + nl;
    }

    //void Match()
    {
      //header
      s += Indent() + "public void Match(" + nl;
      indent += 2;
      foreach (ADTImpl impl in adt.impls) {
        string comma = impl.Equals(adt.impls.Last()) ? "" : ",";
        s += Indent() + "Action<" + impl.name + "> " + impl.name + nullStr + comma + nl;
      }
      indent -= 2;
      s += Indent() + ") {" + nl;

      //body
      indent++;
      for (int i = 0; i < adt.impls.Count; i++) {
        string iName = adt.impls[i].name;
        s += Indent();
        if (i != 0) s += "} else ";
        s += "if (this is " + iName + " s" + i + ") {" + nl;
        indent++;
        s += Indent() + "if (" + iName + " != null) " + iName + "(s" + i + ");" + nl;
        indent--;
      }
      s += Indent() + "}" + nl;
      indent--;
      s += Indent() + "}" + nl;
    }

    indent--;
    s += Indent() + "}" + nl;
    return s;
  }

}

public static class Ext {
  public static string ToLog<T>(this List<T> l, string delim = ", ") {
    return String.Join(delim, l);
  }

  public static string ToLog<T>(this T[] a) {
    if (a == null) return "null";
    return a.ToList().ToLog();
  }
}