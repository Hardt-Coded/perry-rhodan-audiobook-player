using Microsoft.FSharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerryRhodan.AudiobookPlayer.Wpf.Helpers
{
    public static class Extensions
    {
        public static FSharpFunc<T1, FSharpFunc<T2, TResult>> ToFSharpFunc<T1, T2, TResult>(this Func<T1, T2, TResult> func)
        {
            Converter<T1, FSharpFunc<T2, TResult>> conv = value1 =>
            {
                return ToFSharpFunc<T2, TResult>(value2 => func(value1, value2));
            };
            return FSharpFunc<T1, FSharpFunc<T2, TResult>>.FromConverter(conv);
        }

        public static FSharpFunc<T1, TResult> ToFSharpFunc<T1,  TResult>(this Func<T1,  TResult> func)
        {
            Converter<T1, TResult> conv = value1 =>
            {
                return func(value1);
            };
            return FSharpFunc<T1, TResult>.FromConverter(conv);
        }
    }
}
