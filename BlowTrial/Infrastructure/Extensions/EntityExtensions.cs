﻿using BlowTrial.Domain.Tables;
using BlowTrial.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlowTrial.Infrastructure.Extensions
{
    public static class EntityExtensions
    {
        delegate bool TryGetVal<T>(int input, out T output);

        public static void AttachAndMarkModified(this DbContext context, Type setType, IEnumerable<ISharedRecord> newVals)
        {
            if (setType.Namespace == "System.Data.Entity.DynamicProxies")
            {
                setType = setType.BaseType;
            }
            if (!setType.GetInterfaces().Contains(typeof(ISharedRecord)))
            {
                throw new ArgumentException("setType must implement ISharedRecord");
            }
            ExecuteAttachAndMarkModified(context, setType, newVals);
        }

        static void ExecuteAttachAndMarkModified(DbContext context, Type setType, IEnumerable<ISharedRecord> newVals)
        {
            TryGetVal<ISharedRecord> getVal;
            if (newVals.Skip(1).Any())
            {
                var localDict = context.Set(setType).Local.Cast<ISharedRecord>().ToDictionary(k => k.Id);
                getVal = localDict.TryGetValue;
            }
            else
            {
                getVal = delegate(int input, out ISharedRecord output)
                {
                    output = context.Set(setType).Local.Cast<ISharedRecord>().FirstOrDefault(r => r.Id == input);
                    return output != null;
                };
            }


            foreach (ISharedRecord v in newVals)
            {
                if (getVal(v.Id, out ISharedRecord existingEntity))
                {
                    var ent = context.Entry(existingEntity);
                    ent.CurrentValues.SetValues(v);
                    ent.State = System.Data.Entity.EntityState.Modified;
                }
                else
                {
                    context.Set(setType).Attach(v);
                    context.Entry(v).State = EntityState.Modified;
                }
            }
            
        }

        public static void AttachAndMarkModified<T>(this DbContext context, IEnumerable<T> newVals) where T : class, ISharedRecord
        {
            ExecuteAttachAndMarkModified(context, typeof(T), newVals);
        }

        public static void AttachAndMarkModified<T>(this DbContext context, T newVal) where T : class, ISharedRecord
        {
            ExecuteAttachAndMarkModified(context, typeof(T), new T[] { newVal });
        }

        static string GetEntitySetName(Type entityType)
        {
            if (entityType==typeof(VaccineAdministered))
            {
                return "VaccinesAdministered";
            }
            return entityType.Name + 's';
        }
    }
}
