#nullable enable

using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace JustCity.Script.Core.Save;

public static class ReflectSave
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

    private readonly record struct M(string Name, Type Type, Func<object, object?> Get, Action<object, object?> Set);

    public static string ToJson(Resource r)
    {
        return Json.Stringify(ToDict(r), "\t");
    }

    public static void FillFromJson(Resource r, string json)
    {
        Variant parsed = Json.ParseString(json);
        if (parsed.VariantType == Variant.Type.Dictionary)
        {
            FromDict(r, parsed.AsGodotDictionary());
        }
    }

    private static Godot.Collections.Dictionary ToDict(Resource r)
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        foreach (M m in Members(r.GetType()))
        {
            dict[m.Name] = ToVariant(m.Get(r));
        }
        return dict;
    }

    private static void FromDict(Resource r, Godot.Collections.Dictionary d)
    {
        foreach (M m in Members(r.GetType()))
        {
            if (d.TryGetValue(m.Name, out Variant v))
            {
                m.Set(r, FromVariant(v, m.Type));
            }
        }
    }

    private static List<M> Members(Type t)
    {
        List<M> members = [];

        foreach (FieldInfo f in t.GetFields(Flags))
        {
            if (f.GetCustomAttribute<ExportAttribute>() is null) continue;
            members.Add(new M(f.Name, f.FieldType, f.GetValue, (o, v) => f.SetValue(o, v)));
        }

        foreach (PropertyInfo p in t.GetProperties(Flags))
        {
            if (p.GetCustomAttribute<ExportAttribute>() is null) continue;
            if (!p.CanRead) continue;
            members.Add(new M(p.Name, p.PropertyType, p.GetValue, (o, v) =>
            {
                if (p.CanWrite) p.SetValue(o, v);
            }));
        }

        return members;
    }

    private static Variant ToVariant(object? o)
    {
        if (o == null) return default;
        if (o is bool b) return b;
        if (o is int i) return i;
        if (o is long l) return l;
        if (o is float f) return f;
        if (o is double d) return d;
        if (o is string s) return s;
        if (o is Vector2 v) return new Godot.Collections.Dictionary { ["x"] = v.X, ["y"] = v.Y };
        if (o is Resource r) return ToDict(r);
        if (o is IList list)
        {
            Godot.Collections.Array array = new Godot.Collections.Array();
            foreach (object item in list)
            {
                array.Add(ToVariant(item));
            }
            return array;
        }

        string? str = o.ToString();
        return str ?? string.Empty;
    }

    private static object? FromVariant(Variant v, Type t)
    {
        if (t == typeof(bool)) return v.AsBool();
        if (t == typeof(int)) return v.AsInt32();
        if (t == typeof(long)) return v.AsInt64();
        if (t == typeof(float)) return v.AsSingle();
        if (t == typeof(double)) return v.AsDouble();
        if (t == typeof(string)) return v.AsString();

        if (t == typeof(Vector2))
        {
            Godot.Collections.Dictionary d = v.AsGodotDictionary();
            return new Vector2(d["x"].AsSingle(), d["y"].AsSingle());
        }

        if (typeof(Resource).IsAssignableFrom(t))
        {
            Resource r = (Resource)Activator.CreateInstance(t)!;
            FromDict(r, v.AsGodotDictionary());
            return r;
        }

        if (t.IsArray)
        {
            Type e = t.GetElementType()!;
            Godot.Collections.Array s = v.AsGodotArray();
            Array a = Array.CreateInstance(e, s.Count);
            for (int i = 0; i < s.Count; i++)
            {
                a.SetValue(FromVariant(s[i], e), i);
            }
            return a;
        }

        return null;
    }
}