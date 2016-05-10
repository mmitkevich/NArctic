using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NArctic;
using Vexe.Runtime.Extensions;
using Utilities;

namespace NArctic
{
    public class TypedFrame<T> where T :new()
    {
        public static List<PropertyInfo> GetSettableProps(Type t)
        {
            return t
                  .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                  .Where(p => GetPropertySetter(p, t) != null)
                  .ToList();
        }

        public static List<FieldInfo> GetSettableFields(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ToList();
        }

        public static MethodInfo GetPropertySetter(PropertyInfo propertyInfo, Type type)
        {
            if (propertyInfo.DeclaringType == type) return propertyInfo.GetSetMethod(true);
#if COREFX
            return propertyInfo.DeclaringType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Single(x => x.Name == propertyInfo.Name
                        && x.PropertyType == propertyInfo.PropertyType
                        && IsParameterMatch(x.GetIndexParameters(), propertyInfo.GetIndexParameters())
                        ).GetSetMethod(true);
#else
            return propertyInfo.DeclaringType.GetProperty(
                   propertyInfo.Name,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                   Type.DefaultBinder,
                   propertyInfo.PropertyType,
                   propertyInfo.GetIndexParameters().Select(p => p.ParameterType).ToArray(),
                   null).GetSetMethod(true);
#endif
        }

        public Dictionary<string, PropertyInfo> Props;
        public string[] Keys;
        public DataFrame DataFrame;

        public TypedFrame(int count, string[] keys=null)
        {
            var type = typeof(T);
            Props = GetSettableProps(type).ToDictionary(x=>x.Name);
            Keys = keys ?? Props.Keys.ToArray();
            if(count>0)
                DataFrame = CreateDataFrame(count);
        }

        public TypedFrame(DataFrame df, string[] keys = null)
        {
            var type = typeof(T);
            Props = GetSettableProps(type).ToDictionary(x => x.Name);
            Keys = keys ?? Props.Keys.ToArray();
            DataFrame = df;
        }

        public int Enqueue(T value)
        {
            return this.DataFrame.Rows.Enqueue((d, i) => this[i] = value);
        }

        public T Dequeue()
        {
            int r = this.DataFrame.Rows.Dequeue();
            if (r < 0)
                throw new InvalidOperationException("Empty queue");
            return this[r];
        }

        public DataFrame CreateDataFrame(int count)
        {
            var types = Keys.Select(k => Props[k].PropertyType).ToArray();
            var df = new DataFrame(count, types);
            for (int i = 0; i < df.Columns.Count; i++)
                df.Columns[i].Name = Props[Keys[i]].Name;
            DataFrame = df;
            return df;
        }

        public T Get(DataFrame df, int row)
        {
            T obj = new T();
            for(int i=0;i<Keys.Length;i++)
            {
                var pi = Props[Keys[i]];
                if (pi.PropertyType == typeof(double))
                    pi.DelegateForSet<T, double>()(ref obj, df[i].AsDouble.Value[row]);
                else if (pi.PropertyType == typeof(long))
                    pi.DelegateForSet<T, long>()(ref obj, df[i].AsLong.Value[row]);
                else throw new InvalidOperationException("unsupported type {0}".Args(pi.PropertyType));
            }
            return obj;
        }

        public void Set(DataFrame df, int row, T obj)
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                var pi = Props[Keys[i]];
                if (pi.PropertyType == typeof(double))
                    df[i].AsDouble.Value[row] = pi.DelegateForGet<T, double>()(obj);
                else if (pi.PropertyType== typeof(long))
                    df[i].AsLong.Value[row] = pi.DelegateForGet<T, long>()(obj);
                else throw new InvalidOperationException("unsupported type {0}".Args(pi.PropertyType));
            }
        }

        public T this[int row]
        {
            get { return Get(DataFrame, row); }
            set {
                Set(DataFrame, row, value);
            }
        }
        
        public int Count
        {
            get { return DataFrame.Rows.Count; }
        }
    }
}
