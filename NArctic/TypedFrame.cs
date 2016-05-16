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
    public class RingFrame<T>:Ring where T :new()
    {
        public TypedFrame<T> TypedFrame;
        public DataFrame DataFrame {
            get { return TypedFrame.DataFrame;  }
        }

        public RingFrame(TypedFrame<T> tf) : base(()=>tf.DataFrame.Rows.Count)
        {
            this.TypedFrame = tf;
        }
        public int Enqueue(T value)
        {
            var r = base.Enqueue();
            if (r < 0)
                return -1;
            TypedFrame[r] = value;
            return r;
        }

        public bool Add(T value)
        {
            return Enqueue(value) >= 0;
        }

        public T Dequeue()
        {
            int r = base.Dequeue();
            if (r < 0)
                throw new InvalidOperationException("Empty queue");
            return TypedFrame[r];
        }
    }

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
        public string Index;

        public TypedFrame(long count, string index=null, string[] keys=null)
        {
            var type = typeof(T);
            Props = GetSettableProps(type).ToDictionary(x=>x.Name);
            Keys = keys ?? Props.Keys.ToArray();
            Index = index;
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

        public DataFrame CreateDataFrame(long count)
        {
            var types = Keys.Select(k => Props[k].PropertyType).ToArray();
            var df = new DataFrame(count, types, Keys, index:Index);
            df.Index = df.Columns[this.Index];
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
                    SetProperty<T, double>(ref obj, pi,  df[i].As<double>()[row]);
                else if (pi.PropertyType == typeof(long))
                    SetProperty<T, long>(ref obj, pi, df[i].As<long>()[row]);
                else if (pi.PropertyType == typeof(int))
                    SetProperty<T, int>(ref obj, pi, df[i].As<int>()[row]);
                else if (pi.PropertyType == typeof(DateTime))
                    SetProperty<T,DateTime>(ref obj, pi, df[i].As<DateTime>()[row]);

                else throw new InvalidOperationException("unsupported type {0}".Args(pi.PropertyType));
            }
            return obj;
        }

        public Q GetProperty<T, Q>(T obj, PropertyInfo pi)
        {
            return (Q) pi.GetValue(obj);
            //return pi.DelegateForGet<T, Q>()(obj);
        }

        public void SetProperty<T, Q>(ref T obj, PropertyInfo pi, Q value)
        {
            pi.SetValue(obj, value);
            //pi.DelegateForSet<T, Q>()(obj, value);
        }

        public void Set(DataFrame df, int row, T obj)
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                var pi = Props[Keys[i]];
                if (pi.PropertyType == typeof(double))
                    df[i].As<double>()[row] = GetProperty<T, double>(obj, pi);
                else if (pi.PropertyType == typeof(long))
                    df[i].As<long>()[row] = GetProperty<T, long>(obj, pi);
                else if (pi.PropertyType == typeof(int))
                    df[i].As<int>()[row] = GetProperty<T, int>(obj, pi);
                else if (pi.PropertyType == typeof(DateTime))
                    df[i].As<DateTime>()[row] = GetProperty<T, DateTime>(obj, pi);

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

        public RingFrame<T> ToRing()
        {
            return new RingFrame<T>(this);
        }

    }
}
