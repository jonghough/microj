﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.IO;

#if CSSCRIPT
using CSScriptLibrary;
#endif

namespace MicroJ {
   
    public class VerbWithRank {
        public long Monadic;
        public long DyadicX;
        public long DyadicY;
        public Func<AType, AType, AType> DyadicFunc;
        public Func<AType, AType> MonadicFunc;
        public static long Infinite = long.MaxValue;

        public VerbWithRank(Func<AType, AType> monad, Func<AType, AType, AType> dyad, long monadic, long x, long y) {
            MonadicFunc = monad;
            DyadicFunc = dyad;
            Monadic = monadic;
            DyadicX = x;
            DyadicY = y;
        }

    }
   
    public class Verbs {

        public static readonly string[] Words = new[] { "+", "-", "*", "%", "i.", "$", "#", "=", "|:", 
            "|.", "-:", "[", "p:", ",", "<", "!", ";", "q:", "{." , "}.", 
            "<.", ">."};

        public Adverbs Adverbs = null;
        public Conjunctions Conjunctions = null;

        //Func<A<long>, A<JString>, A<JString>> copyFunc;
        //Delegate copyFunc;

        Dictionary<Tuple<string, Type, Type>, Delegate> expressionDict;
        Dictionary<string, VerbWithRank> expressionMap;
        public Verbs() {
            expressionDict = new Dictionary<Tuple<string, Type, Type>, Delegate>();
            expressionMap = new Dictionary<string, VerbWithRank>();
            expressionMap["p:"] = new VerbWithRank(primesm, primes, 0, VerbWithRank.Infinite, VerbWithRank.Infinite);
            expressionMap["q:"] = new VerbWithRank(primesqm, primesq, 0, 0, 0);
        }

        public AType InvokeExpression(string op, AType x, AType y, int generics, object callee = null) {
            var key = new Tuple<string, Type, Type>(op, x.GetType(), y.GetType());
            Delegate d;
            if (!expressionDict.TryGetValue(key, out d)) {
                var calleeType = callee == null ? typeof(Verbs) : callee.GetType();

                MethodInfo meth;
                if (generics == 0) {
                    meth = calleeType.GetMethod(op);
                }
                else if (generics == 1) {
                    meth = calleeType.GetMethod(op).MakeGenericMethod(y.GetType().GetGenericArguments().First());
                }
                else {
                    meth = calleeType.GetMethod(op).MakeGenericMethod(x.GetType().GetGenericArguments().First(), y.GetType().GetGenericArguments().First());
                }
                var par1 = Expression.Parameter(x.GetType());
                var par2 = Expression.Parameter(y.GetType());
                var me = callee == null ? this : callee;
                var instance = Expression.Constant(me);
                var call = Expression.Call(instance, meth, par1, par2);

                d = Expression.Lambda(call, par1, par2).Compile();

                expressionDict[key] = d;
            }
            return (AType)d.DynamicInvoke(x, y);
        }   

        public AType InvokeExpression(string op, AType y, object callee = null) {
            var key = new Tuple<string, Type, Type>(op, null, y.GetType());
            Delegate d;
            if (!expressionDict.TryGetValue(key, out d)) {
                var calleeType = callee == null ? typeof(Verbs) : callee.GetType();

                MethodInfo meth;
                meth = calleeType.GetMethod(op).MakeGenericMethod(y.GetType().GetGenericArguments().First());

                var par1 = Expression.Parameter(y.GetType());
                var me = callee == null ? this : callee;
                var instance = Expression.Constant(me);
                var call = Expression.Call(instance, meth, par1);

                d = Expression.Lambda(call, par1).Compile();

                expressionDict[key] = d;
            }

            return (AType)d.DynamicInvoke(y);
        }

        public A<long> iota<T>(A<T> y) where T : struct {
            var shape = y.Ravel.Cast<long>().ToArray();
            var ascending = shape.All(x => x >= 0);
            long ct = prod(shape);
            var k = Math.Abs(ct);
            var z = new A<long>(k);
            if (y.Rank > 0) { z.Shape = shape.Select(x => Math.Abs(x)).ToArray(); }
            z.Ravel = permutationIdx(z.Shape);
            if (!ascending) {
                for (var i = z.Rank - 1; i >= 0; i--) {
                    if (shape[i] < 0) {
                        var nr = z.Rank - i;
                        var conj = new A<Verb>(0);
                        conj.Ravel[0] = new Verb { op = "|.", conj = "\"", rhs = nr.ToString() };
                        z = Conjunctions.rank1ex<long, long>(conj, z);
                    }
                }
            }
            return z;
        }

        public T Add<T, T2>(T a, T2 b) {
            return (T)((dynamic)a + ((T)(dynamic)b));
        }

        public A<T> math<T>(A<T> x, A<T> y, Func<T, T, T> op) where T : struct {
            var z = new A<T>(y.Ravel.Length, y.Shape);
            if (x.Rank == 0) {
                for (var i = 0; i < y.Ravel.Length; i++) {
                    z.Ravel[i] = op(x.Ravel[0], y.Ravel[i]);
                }
            }
            else {
                for (var i = 0; i < y.Ravel.Length; i++) {
                    z.Ravel[i] = op(x.Ravel[i], y.Ravel[i]);
                }
            }
            return z;
        }

        //dynamic dispatch of math operations -- slowest, around 7x slower
        public A<double> mathmixed(dynamic x, dynamic y, Func<dynamic, dynamic, dynamic> op) {
            var z = new A<double>(y.Ravel.Length, y.Shape);
            if (x.Rank == 0) {
                for (var i = 0; i < y.Ravel.Length; i++) {
                    z.Ravel[i] = op(x.Ravel[0], y.Ravel[i]);
                }
            }
            else {
                for (var i = 0; i < y.Ravel.Length; i++) {
                    z.Ravel[i] = op(x.Ravel[i], y.Ravel[i]);
                }
            }
            return z;
        }

        //convert long to double
        public A<double> mathmixed(A<long> x, A<double> y, Func<double, double, double> op) {
            var z = new A<double>(y.Ravel.Length, y.Shape);
            var newx = new A<double>(0);
            newx.Ravel[0] = ((A<long>)x).Ravel[0];

            if (x.Rank == 0) {
                for (var i = 0; i < y.Ravel.Length; i++) {
                    z.Ravel[i] = op(x.Ravel[0], y.Ravel[i]);
                }
            }
            else {
                for (var i = 0; i < y.Ravel.Length; i++) {
                    z.Ravel[i] = op(x.Ravel[i], y.Ravel[i]);
                }
            }
            return z;
        }

        public A<long> shape(AType y) {
            var v = new A<long>(y.Rank);
            if (y.Rank == 0) { v.Ravel = null; }
            else { v.Ravel = y.Shape; }
            return v;
        }

        public A<long> tally(AType y) {
            var v = new A<long>(0);
            if (y.Rank == 0) { v.Ravel[0] = 1; }
            else { v.Ravel[0] = y.Shape[0]; }
            return v;
        }


        public A<Box> box(AType y) {
            var v = new A<Box>(0);
            v.Ravel[0] = new Box { val = y };
            return v;
        }

        public A<T> copy<T>(A<long> x, A<T> y) where T : struct {
            if (x.Rank == 0) {
                var copies = x.Ravel[0];
                var ct = copies * y.Count;
                var shape = y.Shape != null ? y.Shape : new long[] { 1 };
                shape[0] = shape[0] * copies;

                var v = new A<T>(ct, shape);
                long offset = 0;

                for (var n = 0; n < y.Count; n++) {
                    for (var i = 0; i < copies; i++) {
                        v.Ravel[offset++] = y.Ravel[n];
                    }
                }
                return v;
            }
            else if (x.Rank == 1 && x.Shape[0] == y.Shape[0]) {
                var ct = x.Ravel.Where(xx => xx > 0).Sum();
                var subshape = y.Shape.Skip(1).ToArray();
                var subshapect = AType.ShapeProduct(subshape);
                var shape = new long[] { ct }.Concat(subshape).ToArray();

                var v = new A<T>(shape);
                long offset = 0;
                if (y.GetType() != typeof(A<JString>)) {
                    for(var xi = 0; xi < x.Count; xi++) {                    
                        for (var i = 0; i < x.Ravel[xi]; i++) {
                            for (var k = 0; k < subshapect; k++) {
                                v.Ravel[offset++] = y.Ravel[(xi*subshapect)+k];
                            }
                        }
                    }
                    
                } else {
                    for(var xi = 0; xi < x.Count; xi++) {                    
                        for (var i = 0; i < x.Ravel[xi]; i++) {
                            v.Ravel[offset++] = y.Ravel[xi];
                        }
                    }
                }
                return v;
            }
            else {
                throw new NotImplementedException();
            }
        }

        public A<T> transpose<T>(A<T> y) where T : struct {
            var shape = y.Shape.Reverse().ToArray();
            var v = new A<T>(y.Count, shape);
            var offsets = new long[y.Shape.Length];
            for (var i = 1; i <= y.Shape.Length - 1; i++) {
                offsets[(i - 1)] = prod(y.Shape.Skip(i).ToArray());
            }
            offsets[y.Shape.Length - 1] = 1;
            offsets = offsets.Reverse().ToArray();
            var idx = 0;
            long[] odometer = new long[shape.Length];
            for (var i = 0; i < y.Count; i++) {
                var offset = 0L;
                for (var k = y.Shape.Length - 1; k >= 0; k--) {
                    offset = offset + (offsets[k] * odometer[k]);
                }
                v.Ravel[idx] = y.Ravel[offset];
                idx++;

                odometer[shape.Length - 1]++;

                for (var k = y.Shape.Length - 1; k > 0; k--) {
                    if (odometer[k] == shape[k]) {
                        odometer[k] = 0;
                        odometer[k - 1]++;
                    }
                }
            }
            return v;
        }

        //permutation idx
        public long[] permutationIdx(long[] y, bool asc = true) {
            long tail = y.Length > 1 ? y.Skip(1).Aggregate((a, b) => a * b) : 1;
            var head = y[0];
            var v = new long[head * tail];
            if (asc) {
                for (var i = 0; i < head * tail; i++) {
                    v[i] = i;
                }
            }
            else {
                long offset = head * tail - 1;
                for (var i = 0; i < head; i++) {
                    var max = (i + 1) * tail;
                    for (var k = 0; k < tail; k++) {
                        v[offset--] = max - k - 1;
                    }
                };
            }
            return v;
        }

        public A<T> reverse<T>(A<T> y) where T : struct {
            var v = new A<T>(y.Count, y.Shape);
            var pidx = permutationIdx(y.Shape, false);
            for (var i = 0; i < y.Count; i++) {
                v.Ravel[i] = y.Ravel[pidx[i]];
            }
            return v;
        }

        public A<JString> reverse_str(A<JString> y) {
            var v = new A<JString>(y.Count, y.Shape);
            var ct = y.Count;

            for (var i = 0; i < y.Count; i++) {
                var str = new string(y.Ravel[ct - i - 1].str.ToCharArray().Reverse().ToArray());
                v.Ravel[i].str = String.Intern(str);
            }
            return v;
        }

        public long prod(long[] ri) {
            return ri.Aggregate(1L, (prod, next) => prod * next);
        }

        public A<T2> reshape<T2>(A<long> x, A<T2> y) where T2 : struct {

            var ct = prod(x.Ravel);
            long offset = 0;
            var ylen = y.Count;
            var v = new A<T2>((int)ct, x.Ravel);
            for (var i = 0; i < ct; i++) {
                v.Ravel[i] = y.Ravel[offset];
                offset++;
                if (offset > ylen - 1) { offset = 0; }
            }
            return v;
        }

        public A<JString> reshape_str(A<long> x, A<JString> y) {
            var ct = prod(x.Ravel);
            long offset = 0;
            var ylen = y.Count;

            char[] chars = new char[ct];
            ylen = y.Ravel[0].str.Length;

            for (var i = 0; i < ct; i++) {
                chars[i] = y.Ravel[0].str[(int)offset];
                offset++;
                if (offset > ylen - 1) { offset = 0; }
            }
            var size = x.Rank >= 1 ? x.Ravel[0] : 1;
            var len = x.Rank >= 1 ? x.Ravel[x.Rank] : x.Ravel[0];
            var v = new A<JString>(size, new[] { size, len });
            for (var i = 0; i < size; i++) {
                //intern string saves 4x memory in simple test and 20% slower
                v.Ravel[i].str = String.Intern(new String(chars, (int)(i * len), (int)len));
                //v.Ravel[i].str =  new String(chars, (int)(i*len), (int)len);
            }

            return v;
        }

        public A<bool> equals<T, T2>(A<T> x, A<T2> y) where T : struct where T2 : struct {
            //todo handle application errors without exceptions
            if (x.Count != y.Count) { throw new ArgumentException("Length Error"); }

            var z = new A<bool>(y.Count);
            for (var i = 0; i < y.Count; i++) {
                //todo: need a faster way to compare equality, this was failing on double to long comparisons
                //x.Ravel[i].Equals(y.Ravel[i]);
                z.Ravel[i] = x.StringConverter(x.Ravel[i]) == y.StringConverter(y.Ravel[i]);
            }
            return z;
        }

        public A<long> indexof<T>(A<T> x, A<T> y) where T : struct {            
            var z = new A<long>(y.Count);
            if (y.Rank <= 1 && x.Rank <= 1) {
                for (var i = 0; i < y.Count; i++) {
                    z.Ravel[i] = x.Count;
                    for (var xi = 0; xi < x.Count; xi++) {
                        if (x.Ravel[xi].Equals(y.Ravel[i])) {
                            z.Ravel[i] = xi;
                            break;
                        }
                    }
                }
            }
            else {
                for (var i = 0; i < y.Count; i++) {
                    z.Ravel[i] = x.Count;
                    string yval = y.StringConverter(y.Ravel[i]);
                    for (var xi = 0; xi < x.Count; xi++) {
                        //todo: need a faster way to compare equality
                        if (x.StringConverter(x.Ravel[xi]) == yval) {
                            z.Ravel[i] = xi;
                            break;
                        }
                    }
                }
            }
            
            return z;
        }

        public A<T> ravel<T>(A<T> y) where T : struct {
            var v = new A<T>(y.Count);
            for (var n = 0; n < y.Count; n++) {
                v.Ravel[n] = y.Ravel[n];
            }
            return v;
        }

        public A<long> floor<T>(AType y) where T : struct {            
            if (y.GetType() == typeof(A<long>)) {
                A<long> ay = (A<long>)y;
                var v = new A<long>(ay.Count, ay.Shape);                
                for (var n = 0; n < v.Count; n++) {
                    v.Ravel[n] = ay.Ravel[n];
                }
                return v;
            }
            else if (y.GetType() == typeof(A<double>)) {
                A<double> ay = (A<double>)y;
                var v = new A<long>(ay.Count, ay.Shape);                
                for (var n = 0; n < v.Count; n++) {
                    v.Ravel[n] = (long)Math.Floor(ay.Ravel[n]);
                }
                return v;
            }
            else {
                throw new DomainException();
            }
        }

        public A<long> ceiling<T>(AType y) where T : struct {                        
            if (y.GetType() == typeof(A<long>)) {
                A<long> ay = (A<long>)y;
                var v = new A<long>(ay.Count, ay.Shape);
                for (var n = 0; n < v.Count; n++) {
                    v.Ravel[n] = ay.Ravel[n];
                }
                return v;
            }
            else if (y.GetType() == typeof(A<double>)) {
                A<double> ay = (A<double>)y;
                var v = new A<long>(ay.Count, ay.Shape);
                for (var n = 0; n < v.Count; n++) {
                    v.Ravel[n] = (long)Math.Ceiling(ay.Ravel[n]);
                }
                return v;
            }
            else {
                throw new DomainException();
            }
        }

        public A<T> head<T>(A<T> y) where T : struct {
            long[] newShape = null;
            if (y.Shape != null) { newShape = y.Shape.Skip(1).ToArray(); }
            var v = new A<T>(y.ShapeProduct(skip: 1), newShape);
            v.Ravel = y.Copy(v.Count > 0 ? v.Count : 1);
            return v;
        }

        public A<T> behead<T>(A<T> y) where T : struct {
            long[] newShape = y.Shape;
            newShape[0] = newShape[0] - 1;           
            var v = new A<T>(newShape);
            v.Ravel = y.Copy(v.Count > 0 ? v.Count : 1, skip: y.ShapeProduct(skip: 1));
            return v;
        }

        public A<T> take<T>(A<long> x, A<T> y) where T : struct {
            long[] newShape = null;
            
            if (x.Rank > 0) { throw new NotImplementedException("Rank > 0 not implemented on take"); }
            var xct = Math.Abs(x.Ravel[0]);
            if (y.Shape != null) { newShape = new long[] { xct }.Concat(y.Shape.Skip(1)).ToArray(); }
            var v = new A<T>(newShape);
            v.Ravel = y.Copy(v.Count, ascending: x.Ravel[0] >= 0);
            return v;
        }


        public A<T> drop<T>(A<long> x, A<T> y) where T : struct {
            long[] newShape = null;

            if (x.Rank > 0) { throw new NotImplementedException("Rank > 0 not implemented on take"); }
            var xct = Math.Abs(x.Ravel[0]);
            if (y.Shape != null) { 
                newShape = y.Shape;
                newShape[0] -= xct;
            }
            var v = new A<T>(newShape);
            var skip = y.Count- AType.ShapeProduct(newShape);
            v.Ravel = y.Copy(v.Count, skip: skip, ascending: x.Ravel[0] >= 0);
            return v;
        }

        public AType raze<T>(A<Box> y) where T : struct {
            Type type = null;
            long totalCount = 0;
            for (var i = 0; i < y.Count; i++) {
                var thisType = y.Ravel[i].val.GetType();
                if (type != null && type != thisType) {
                    throw new DomainException();
                }
                if (y.Ravel[i].val.Shape != null) {
                    totalCount += AType.ShapeProduct(y.Ravel[i].val.Shape);
                }
                else {
                    totalCount += 1;
                }
                type = thisType;
            }
            if (type == typeof(A<long>)) {
                if (y.Count == 1) {
                    var z = new A<long>(totalCount, y.Ravel[0].val.Shape);
                    long offset = 0;
                    for (var i = 0; i < y.Count; i++) {
                        for (var k = 0; k < y.Ravel[i].val.GetCount(); k++) {
                            z.Ravel[offset++] = ((A<long>)y.Ravel[i].val).Ravel[k];
                        }
                    }
                    return z;
                }
                else {
                    var op = new A<Verb>(0);
                    op.Ravel[0] = new Verb { op = "," };
                    return Adverbs.reduceboxed<long>(op, y);
                }
            }
            return null;
        }

        public A<T> append<T>(A<T> x, A<T> y) where T : struct {
            if (x.Rank > 1 && AType.ShapeProduct(x.Shape) != AType.ShapeProduct(y.Shape)) throw new NotImplementedException("Rank > 1 non-equal shapes not implemented yet (need framing fill)");

            long[] newShape;
            newShape = new long[] { x.Count + y.Count };

            if (y.Rank > 0) {
                var tail = y.Shape.Skip(1).ToArray();
                var xframe = x.Rank > 1 ? x.Shape[0] : 1;
                newShape = new long[] { xframe + y.Shape[0] }.Concat(tail).ToArray();
            }

            var v = new A<T>(x.Count + y.Count, newShape);
            var offset = 0;
            for (var n = 0; n < x.Count; n++) {
                v.Ravel[offset++] = x.Ravel[n];
            }
            for (var n = 0; n < y.Count; n++) {
                v.Ravel[offset++] = y.Ravel[n];
            }

            return v;
        }

        public AType primesm(AType w) {
            return primes(null, w);
        }
        public AType primes(AType a, AType w) {
            var x = a != null ? (A<long>)a : new A<long>(0);
            var y = (A<long>)w;
            var xv = x.Ravel[0];
            Func<long, long> fl = null;
            Func<long, bool> fb = null;
            Func<long, long[]> fls = null;
            long[] frame = null;
            if (a == null) fl = Primes.GetNthPrime;
            else if (xv == -1) fl = (l) => Primes.Pi((float)l);
            else if (xv == 0) fb = (l) => !Primes.IsPrime(l);
            else if (xv == 1) fb = Primes.IsPrime;
            else if (xv == 2) {
                frame = new long[] { 2 };
                fls = (l) => Primes.GetFactorsWithExponents(Primes.Factor(l).ToList()).ToArray();
            }

            else if (xv == 3) fls = (l) => Primes.Factor(l);

            if (fl != null) return y.Apply(fl);
            else if (fb != null) return y.Apply(fb);
            else if (fls != null) return y.Apply(fls, frame);

            else throw new NotImplementedException();
        }
        public AType primesqm(AType w) {
            return primesq(null, w);
        }
        public AType primesq(AType a, AType w) {
            var x = a != null ? (A<long>)a : new A<long>(0);
            var y = (A<long>)w;
            var xv = x.Ravel[0];
            Func<long, long> fl = null;
            Func<long, bool> fb = null;
            Func<long, long[]> fls = null;
            long[] frame = null;
            if (a == null) fls = (l) => Primes.Factor(l);
            else if ((long)xv <= Int32.MaxValue && xv > 0) {
                //  frame = new long[] { 2 };
                fls = (l) => {
                    Dictionary<long, int> facs = Primes.GetFactorExponents(Primes.Factor(l).ToList());
                    List<long> ps = Primes.atkinSieve(1 + (int)l); // first xv primes
                    List<long> pps = new List<long>(); //prime powers
                    int i = 0;
                    foreach (long p in ps) {
                        if (facs.ContainsKey(p)) {
                            pps.Add(facs[p]);
                        }
                        else pps.Add(0);
                        i++;
                        if (i >= (int)xv) break;
                    }
                    while (pps.Count < (int)xv)
                        pps.Add(0);
                    return pps.ToArray();
                };
            }

            if (fl != null) return y.Apply(fl);
            else if (fb != null) return y.Apply(fb);
            else if (fls != null) return y.Apply(fls, frame);

            else throw new NotImplementedException();
        }


        public AType Call2(AType method, AType x, AType y) {
            var verb = ((A<Verb>)method).Ravel[0];
            if (verb.adverb != null) {
                return Adverbs.Call2(method, x, y);
            }

            //future: add check for integrated rank support
            if (verb.conj != null) {
                return Conjunctions.Call2(method, x, y);
            }

            var op = verb.op;

            VerbWithRank verbWithRank = null;

            if (op == "+") {
                if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<long>)) {
                    return math((A<long>)x, (A<long>)y, (a, b) => a + b);
                }
                else if (x.GetType() == typeof(A<double>) && y.GetType() == typeof(A<double>)) {
                    return math((A<double>)x, (A<double>)y, (a, b) => a + b);
                }
                else if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<double>)) {
                    return mathmixed((A<long>)x, (A<double>)y, (a, b) => a + b);
                }
                else if (x.GetType() != y.GetType()) {
                    return mathmixed(x, y, (a, b) => a + b);
                }

            }
            else if (op == "-") {
                if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<long>)) {
                    return math((A<long>)x, (A<long>)y, (a, b) => a - b);
                }
                else if (x.GetType() == typeof(A<double>) && y.GetType() == typeof(A<double>)) {
                    return math((A<double>)x, (A<double>)y, (a, b) => a - b);
                }
                else if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<double>)) {
                    return mathmixed((A<long>)x, (A<double>)y, (a, b) => a - b);
                }
                else if (x.GetType() != y.GetType()) {
                    return mathmixed(x, y, (a, b) => a - b);
                }
            }
            else if (op == "*") {
                if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<long>)) {
                    return math((A<long>)x, (A<long>)y, (a, b) => a * b);
                }
                else if (x.GetType() == typeof(A<double>) && y.GetType() == typeof(A<double>)) {
                    return math((A<double>)x, (A<double>)y, (a, b) => a * b);
                }
                else if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<double>)) {
                    return mathmixed((A<long>)x, (A<double>)y, (a, b) => a * b);
                }
                else if (x.GetType() != y.GetType()) {
                    return mathmixed(x, y, (a, b) => a * b);
                }
            }
            else if (op == "<.") {
                if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<long>)) {
                    return math((A<long>)x, (A<long>)y, (a, b) => a < b ? a : b);
                }
                else if (x.GetType() == typeof(A<double>) && y.GetType() == typeof(A<double>)) {
                    return math((A<double>)x, (A<double>)y, (a, b) => a < b ? a : b);
                }
                else if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<double>)) {
                    return mathmixed((A<long>)x, (A<double>)y, (a, b) => a < b ? a : b);
                }
                else if (x.GetType() != y.GetType()) {
                    return mathmixed(x, y, (a, b) => a < b ? a : b);
                }
            }
            else if (op == ">.") {
                if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<long>)) {
                    return math((A<long>)x, (A<long>)y, (a, b) => a > b ? a : b);
                }
                else if (x.GetType() == typeof(A<double>) && y.GetType() == typeof(A<double>)) {
                    return math((A<double>)x, (A<double>)y, (a, b) => a > b ? a : b);
                }
                else if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<double>)) {
                    return mathmixed((A<long>)x, (A<double>)y, (a, b) => a > b ? a : b);
                }
                else if (x.GetType() != y.GetType()) {
                    return mathmixed(x, y, (a, b) => a > b ? a : b);
                }
            }
            else if (op == "%") {
                var a2 = x.ConvertDouble();
                var b2 = y.ConvertDouble();
                return math(a2, b2, (a, b) => a / b);
            }
            else if (op == "$") {
                if (x.GetType() == typeof(A<long>)) {
                    if (y.GetType() == typeof(A<long>)) {
                        return reshape((A<long>)x, (A<long>)y);
                    }
                    else if (y.GetType() == typeof(A<double>)) {
                        return reshape((A<long>)x, (A<double>)y);
                    }
                    else if (y.GetType() == typeof(A<JString>)) {
                        return reshape_str((A<long>)x, (A<JString>)y);
                    }
                }
            }
            else if (op == "=") {
                if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<long>))
                    return equals((A<long>)x, (A<long>)y);
                else if (x.GetType() == typeof(A<double>) && y.GetType() == typeof(A<double>))
                    return equals((A<double>)x, (A<double>)y);
                else if (x.GetType() == typeof(A<long>) && y.GetType() == typeof(A<double>))
                    return equals((A<long>)x, (A<double>)y);
                else if (x.GetType() == typeof(A<JString>) && y.GetType() == typeof(A<JString>))
                    return equals((A<JString>)x, (A<JString>)y);
                else return InvokeExpression("equals", x, y, 2);
            }
            else if (op == "#") {
                return InvokeExpression("copy", x, y, 1);
            }
            else if (op == "i.") {
                return InvokeExpression("indexof", x, y, 1);
            }
            else if (op == ",") {
                return InvokeExpression("append", x, y, 1);
            }
            else if (op == "{.") {
                return InvokeExpression("take", x, y, 1);
            }
            else if (op == "}.") {
                return InvokeExpression("drop", x, y, 1);
            }
            else if (op == "-:") {
                //temporary
                var z = new A<bool>(0);
                z.Ravel[0] = x.ToString() == y.ToString();
                return z;
            }
            else if (expressionMap.TryGetValue(op, out verbWithRank)) {
                if (verbWithRank.DyadicX == VerbWithRank.Infinite && verbWithRank.DyadicY == VerbWithRank.Infinite) {
                    return verbWithRank.DyadicFunc(x, y);
                }
                else if (verbWithRank.DyadicX == 0 && verbWithRank.DyadicY == 0) {
                    return verbWithRank.DyadicFunc(x, y);
                }
            }

            throw new NotImplementedException(op + " on x:" + x + " y:" + y + " type: " + y.GetType());
        }

        //candidate for code generation
        public AType Call1(AType method, AType y) {
            var verb = ((A<Verb>)method).Ravel[0];
            if (verb.adverb != null) {
                return Adverbs.Call1(method, y);
            }

            //future: add check for integrated rank support
            if (verb.conj != null) {
                return Conjunctions.Call1(method, y);
            }

            var op = verb.op;
            VerbWithRank verbWithRank = null;

            if (op == "i.") {
                if (y.GetType() == typeof(A<int>)) {
                    return iota((A<int>)y);
                }
                else if (y.GetType() == typeof(A<long>)) {
                    return iota((A<long>)y);
                }
            }
            else if (op == "$") {
                return shape(y);
            }
            else if (op == "#") {
                return tally(y);
            }
            else if (op == "{.") {
                return InvokeExpression("head", y);
            }
            else if (op == "}.") {
                return InvokeExpression("behead", y);
            }
            else if (op == "<") {
                return box(y);
            }

            else if (op == "|:") {
                if (y.GetType() == typeof(A<int>)) {
                    return transpose((A<int>)y);
                }
                else if (y.GetType() == typeof(A<long>)) {
                    return transpose((A<long>)y);
                }
            }
            else if (op == "|.") {
                if (y.GetType() == typeof(A<long>)) {
                    return reverse((A<long>)y);
                }
                else if (y.GetType() == typeof(A<JString>)) {
                    return reverse_str((A<JString>)y);
                }
                return InvokeExpression("reverse", y);
            }
            else if (op == ",") {
                return InvokeExpression("ravel", y);
            }
            else if (op == "<.") {
                return InvokeExpression("floor", y);
            }
            else if (op == ">.") {
                return InvokeExpression("ceiling", y);
            }

            else if (op == ";") {
                if (y.GetType() == typeof(A<Box>)) {
                    return raze<Box>((A<Box>)y);
                }
                else {
                    //raze seems to be like ravel for non-boxed
                    return InvokeExpression("ravel", y);
                }
            }
            else if (op == "!") {
                A<double> a = new A<double>(1);
                if (y is A<int> || y is A<long>)
                    a.Ravel[0] = Gamma.GammaReal(1.0 * (((A<long>)y).Ravel[0]));
                else a.Ravel[0] = Gamma.GammaReal(1.0 * (((A<double>)y).Ravel[0]));
                return a;
            }
            else if (expressionMap.TryGetValue(op, out verbWithRank)) {
                return verbWithRank.MonadicFunc(y);
            }
            throw new NotImplementedException(op + " on y: " + y + " type: " + y.GetType());
        }
    }
    public class Conjunctions {
        public static readonly string[] Words = new[] { "\"", "!:", "&", ":" };
        public Verbs Verbs;
        public Dictionary<string, AType> Names;

        public Conjunctions(Verbs verbs) {
            Verbs = verbs;

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            AppDomain currentDomain = AppDomain.CurrentDomain;

            var binPath = Path.Combine(path, "bin");
            currentDomain.AssemblyResolve += new ResolveEventHandler((sender, args) => {
                var assemblyPath = Path.Combine(binPath, new AssemblyName(args.Name).Name + ".dll");
                if (File.Exists(assemblyPath) == false) return null;
                Assembly dependency = Assembly.LoadFrom(assemblyPath);
                return dependency;
            });
        }

        public A<T2> rank1ex<T, T2>(AType method, A<T> y)
            where T : struct
            where T2 : struct {
            var verb = ((A<Verb>)method).Ravel[0];
            var newRank = Convert.ToInt32(verb.rhs);

            //create a new verb without the conj component so we can safely pass it around
            var newVerb = new A<Verb>(1);
            if (verb.childVerb != null) {
                Verb cv = (Verb)verb.childVerb;
                newVerb.Ravel[0] = cv;
            }
            else {
                newVerb.Ravel[0] = new Verb { op = verb.op, adverb = verb.adverb };
            }

            if (newRank == y.Rank) { return (A<T2>)Verbs.Call1(newVerb, y); }

            var shape = y.Shape;
            bool isString = y.GetType() == typeof(A<JString>);

            if (isString) {
                //drop the last part of the shape for strings, since it's an indirect reference
                shape = y.Shape.Take(y.Shape.Length - 1).ToArray();
            }
            var newShape = shape.Take(y.Rank - newRank).ToArray();


            var newCt = AType.ShapeProduct(newShape);
            var vs = new A<T2>[newCt];
            var subShape = shape.Skip(y.Rank - newRank).ToArray();
            var subShapeCt = AType.ShapeProduct(subShape);
            var offset = 0;

            for (var i = 0; i < vs.Length; i++) {
                var newY = new A<T>(subShapeCt, subShape);
                for (var k = 0; k < newY.Count; k++) {
                    if (!isString || newRank > 0) {
                        newY.Ravel[k] = y.Ravel[offset];
                    }
                    else {
                        newY.Ravel[k] = (T)(object)y.GetCharJString(offset);
                    }
                    offset++;
                }
                vs[i] = (A<T2>)Verbs.Call1(newVerb, newY);
            }
            var ct = vs.Length * vs[0].Count;

            if (vs[0].Shape != null) {
                newShape = newShape.Concat(vs[0].Shape).ToArray();
            }
            var v = new A<T2>(ct, newShape);
            offset = 0;
            for (var i = 0; i < vs.Length; i++) {
                for (var k = 0; k < vs[0].Count; k++) {
                    v.Ravel[offset++] = vs[i].Ravel[k];
                }
            }
            return v;
        }

        //to use interop, download https://csscriptsource.codeplex.com/releases/view/614904
        //and put CSScriptLibrary.dll and Mono.CSharp.dll into the bin folder (relative to the exe)
        //future: add a boxed method that can take parameters
        //(3 2 $ 'abc')  (150!:0) 'return v.ToString();'
        //(3 2 $ 'abc')  (150!:0) 'return v.Ravel[0].str;'
        //(3 2 $ 'abc')  (150!:0) 'return v.Rank.ToString();'
        //(3 2 $ 1)  (150!:0) 'return v.ToString();'
        //'' (150!:0) 'System.Diagnostics.Debugger.Break();'
        //should the code be x or y?
        Dictionary<string, Func<AType, string>> dotnetMethodCache = null;
        public A<JString> calldotnet<T>(A<T> x, A<JString> y) where T : struct {

#if CSSCRIPT
            if (dotnetMethodCache == null) { dotnetMethodCache = new Dictionary<string, Func<AType, string>>(); }
            Func<AType, string> func = null;
            if (!dotnetMethodCache.TryGetValue(y.Ravel[0].str, out func)) {
                var code = y.Ravel[0].str;
                var lines = code.Split('\n');
                var usings = String.Join("\n", lines.Where(t => t.StartsWith("//css_using ")).Select(t => "using " + t.Replace("//css_using ", "") + ";").ToArray());
                var refs = lines.Where(t => t.StartsWith("//css_ref ")).SelectMany(t => t.Replace("//css_ref ", "").Split(',')).Select(t => t.Trim()).ToArray();
                func = CSScript.LoadDelegate<Func<AType, string>>(usings + "\n" + "string func (MicroJ.AType v) { " + code + " }", null, false, refs);
                dotnetMethodCache[y.Ravel[0].str] = func;
            }
            var ret = func(x);
            var v = new A<JString>(0);
            v.Ravel[0] = new JString { str = ret };
            return v;
#else
            var v = new A<JString>(0);
            v.Ravel[0] = new JString { str = "microj must be compiled with csscript support" };
            return v;
#endif

        }

        public AType Call1(AType method, AType y) {
            var verb = ((A<Verb>)method).Ravel[0];

            //rank
            if (verb.conj == "\"") {
                //future: add special code for +/"n or use some type of integrated rank support

                //not sure if this is the right way to deal with boxes yet
                if (verb.childVerb != null && ((Verb)verb.childVerb).op == "<") {
                    if (y.GetType() == typeof(A<long>)) {
                        return rank1ex<long, Box>(method, (A<long>)y);
                    }
                    else if (y.GetType() == typeof(A<JString>)) {
                        return rank1ex<JString, Box>(method, (A<JString>)y);
                    }
                    else if (y.GetType() == typeof(A<double>)) {
                        return rank1ex<double, Box>(method, (A<double>)y);
                    }
                    else if (y.GetType() == typeof(A<bool>)) {
                        return rank1ex<bool, Box>(method, (A<bool>)y);
                    }


                }
                if (y.GetType() == typeof(A<long>)) { return rank1ex<long, long>(method, (A<long>)y); }
                //todo: evaluate performance of dynamic dispatch of rank -- probably ok
                else return Verbs.InvokeExpression("rank1ex", method, y, 1, this);
            }
            //bond
            else if (verb.conj == "&") {
                var x = AType.MakeA(verb.rhs, Names);
                var newVerb = new A<Verb>(0);
                newVerb.Ravel[0] = (Verb)verb.childVerb;
                //todo this order may not be right
                return Call2(newVerb, y, x);

            }
            else if (verb.conj == ":" && verb.op == "0") {
                if (verb.rhs == "0") {
                    var v = new A<JString>(0);
                    v.Ravel[0] = new JString { str = y.ToString() };
                    return v;
                }
            }
            throw new NotImplementedException(verb + " on y:" + y + " type: " + y.GetType());
        }

        public AType Call2(AType method, AType x, AType y) {
            var verb = ((A<Verb>)method).Ravel[0];
            if (verb.conj == "!:" && verb.op == "150") {
                if (x.GetType() == typeof(A<JString>)) {
                    return (A<JString>)calldotnet((A<JString>)x, (A<JString>)y);
                }
                else if (x.GetType() == typeof(A<long>)) {
                    return (A<JString>)calldotnet((A<long>)x, (A<JString>)y);
                }
            }
            throw new NotImplementedException(verb + " on y:" + y + " type: " + y.GetType());
        }

    }
    public class Adverbs {
        public static readonly string[] Words = new[] { "/" };
        public Verbs Verbs;
        public Conjunctions Conjunctions;

        public Adverbs(Verbs verbs) {
            Verbs = verbs;
        }

        //todo move to utility
        public long prod(long[] ri) {
            return ri.Aggregate(1L, (prod, next) => prod * next);
        }

        //special code for +/ rank 1 (long)
        public A<long> reduceplus(A<long> y) {
            var v = new A<long>(0);
            long total = 0;
            for (var i = 0; i < y.Count; i++) {
                total += y.Ravel[i];
            }
            v.Ravel[0] = total;
            return v;
        }

        //special code for +/ rank 1 (double)
        public A<double> reduceplus(A<double> y) {
            var v = new A<double>(0);
            double total = 0;
            for (var i = 0; i < y.Count; i++) {
                total += (double)y.Ravel[i];
            }
            v.Ravel[0] = total;
            return v;
        }

        public A<T> reduceboxed<T>(AType op, A<Box> y) where T : struct {
            if (y.Rank == 1) {
                var v = new A<T>(0);
                v = (A<T>)Verbs.Call2(op, y.ToAtom(y.Count - 2).Ravel[0].val, y.ToAtom(y.Count - 1).Ravel[0].val);
                for (var i = y.Count - 3; i >= 0; i--) {
                    v = (A<T>)Verbs.Call2(op, y.ToAtom(i).Ravel[0].val, v);
                }
                return v;
            }
            throw new NotImplementedException();
        }

        public A<T> reduce<T>(AType op, A<T> y) where T : struct {
            if (y.Rank == 1) {
                var v = new A<T>(0);
                v = (A<T>)Verbs.Call2(op, y.ToAtom(y.Count - 2), y.ToAtom(y.Count - 1));
                for (var i = y.Count - 3; i >= 0; i--) {
                    v = (A<T>)Verbs.Call2(op, y.ToAtom(i), v);
                }
                return v;
            }
            else {
                var newShape = y.Shape.Skip(1).ToArray();
                var ct = prod(newShape);
                var v = new A<T>(ct, newShape);
                for (var i = ct - 1; i >= 0; i--) {
                    for (var k = y.Shape[0] - 1; k >= 0; k--) {
                        var n = i + (k * ct);
                        if (k == y.Shape[0] - 1) {
                            var np = i + ((k - 1) * ct);
                            v.Ravel[i] = A<T>.GetAtom(Verbs.Call2(op, y.ToAtom(np), y.ToAtom(n)), 0);
                            k--;
                        }
                        else {
                            v.Ravel[i] = A<T>.GetAtom(Verbs.Call2(op, y.ToAtom(n), v.ToAtom(i)), 0);
                        }
                    }
                }
                return v;
            }
            throw new NotImplementedException();
        }

        public A<T> table<T>(AType op, A<T> x, A<T> y) where T : struct {
            var ct = x.Count * y.Count;
            var shape = new[] { x.Count, y.Count };
            var v = new A<T>(ct, shape);
            long offset = 0;
            for (var xi = 0; xi < x.Count; xi++) {
                for (var yi = 0; yi < y.Count; yi++) {
                    var xt = new A<T>(0);
                    xt.Ravel[0] = x.Ravel[xi];

                    var yt = new A<T>(0);
                    yt.Ravel[0] = y.Ravel[yi];

                    v.Ravel[offset] = ((A<T>)Verbs.Call2(op, xt, yt)).Ravel[0];
                    offset++;
                }
            }
            return v;
        }
        public AType Call1(AType method, AType y) {
            var verb = ((A<Verb>)method).Ravel[0];
            var adverb = verb.adverb;
            var op = verb.op;

            //create a new verb without the adverb component so we can safely pass it around
            var newVerb = new A<Verb>(0);
            newVerb.Ravel[0] = new Verb { op = op };

            //future: add check for integrated rank support (e.g. +/"1)
            if (verb.conj != null) {
                return Conjunctions.Call1(method, y);
            }

            //special code for +/
            if (adverb == "/" && op == "+" && y.Rank == 1 && y.GetType() == typeof(A<long>)) {
                return reduceplus((A<long>)y);
            }
            else if (adverb == "/" && op == "+" && y.Rank == 1 && y.GetType() == typeof(A<double>)) {
                return reduceplus((A<double>)y);
            }
            else if (adverb == "/" && op == "%" && y.GetType() == typeof(A<long>)) {
                //special code to convert longs to double for division
                var newY = y.ConvertDouble();
                return reduce<double>(newVerb, (A<double>)newY);
            }
            else if (adverb == "/") {
                if (y.GetType() == typeof(A<long>)) {
                    return reduce<long>(newVerb, (A<long>)y);
                }
                else if (y.GetType() == typeof(A<double>)) {
                    return reduce<double>(newVerb, (A<double>)y);
                }
            }
            throw new NotImplementedException(adverb + " on y:" + y + " type: " + y.GetType());
        }

        public AType Call2(AType verb, AType x, AType y) {
            var adverb = ((A<Verb>)verb).Ravel[0].adverb;
            var op = ((A<Verb>)verb).Ravel[0].op;

            //create a new verb without the adverb component so we can safely pass it around
            var newVerb = new A<Verb>(0);
            newVerb.Ravel[0] = new Verb { op = op };

            if (adverb == "/" && y.GetType() == typeof(A<long>) && x.GetType() == typeof(A<long>)) {
                return table(newVerb, (A<long>)x, (A<long>)y);
            }

            throw new NotImplementedException("ADV: " + adverb + " on x:" + x + " y:" + y + " type: " + y.GetType());
        }

    }

    public static class Primes {
        public static bool IsPrime(long n) {
            if (n <= 1)
                return false; //should fail?

            if (n == 2)
                return true;

            if (n % 2 == 0)
                return false;

            for (int i = 3; i < Math.Sqrt(n) + 1; i += 2) {
                if (n % i == 0) {
                    return false;
                }
            }
            return true;
        }

        public static long GetNthPrime(long n) {

            if (n < 0)
                return 0;// TODO throw an exception ??
            if (n == 0)
                return 2;
            long prime = 2;
            long i = 0;

            long count = 3; // start at 3
            while (i < n) {
                if (IsPrime(count) == true) {
                    prime = count;
                    i++;
                }
                count += 2;
            }
            return prime;
        }

        public static long Pi(float n) {
            if (n <= 1)
                return 0;
            if (n < 3)
                return 1;
            long c = Pi((float)Math.Pow(n, 1.0f / 3));

            long mu = Pi((float)Math.Sqrt(n)) - c;
            return (long)(phi(n, c) + c * (mu + 1) + (mu * mu - mu) * 0.5f - 1 - SumPi(n, c, mu));
        }

        private static long SumPi(float m, long n, long mu) {
            long i = 1;
            long total = 0;
            while (i <= mu) {
                total += Pi(m / GetNthPrime(n + i - 1));
                i++;
            }
            return total;
        }

        private static long phi(float m, long n) {
            if (m < 0 || n < 0)
                throw new System.Exception("Arguments must be non-negative");
            if (n == 0) {
                return (long)m;
            }
            else {
                return phi(m, n - 1) - phi((long)(m / (float)(GetNthPrime(n - 1))), n - 1);
            }
        }

        public static long[] Factor(long n) {
            List<long> lst = new List<long>();
            if (n <= 1)
                return lst.ToArray(); //throw exception?

            long divisor = PollardRho(n, new Random());
            lst.Add(divisor);
            lst.AddRange(Factor(n / divisor));
            return lst.ToArray();
        }

        //can be used for 2 p: y, possibly.
        public static Dictionary<long, int> GetFactorExponents(List<long> factorList) {
            Dictionary<long, int> d = new Dictionary<long, int>();
            foreach (long l in factorList) {
                if (d.ContainsKey(l))
                    d[l]++;
                else
                    d[l] = 1;
            }
            return d;
        }

        public static List<long> GetFactorsWithExponents(List<long> factorList) {
            Dictionary<long, int> exponentDict = GetFactorExponents(factorList);
            List<long> keys = new List<long>();
            List<long> vals = new List<long>();
            foreach (KeyValuePair<long, int> kvp in exponentDict) {
                keys.Add(kvp.Key);
                vals.Add((long)kvp.Value);
            }
            keys.AddRange(vals);
            return keys;
        }


        //unused.
        private static long FactorizeSimple(long n, long previous) {
            if (n % 2 == 0)
                return 2;
            if (n % 3 == 0)
                return 3;
            if (n % 5 == 0)
                return 5;

            long i;
            for (i = previous; i < Math.Sqrt(n) + 1; i += 2) {
                if (n % i == 0)
                    return i;
            }
            return n;
        }

        private static long PollardRho(long n, Random rand) {

            if (n % 2 == 0)
                return 2;
            if (n % 3 == 0)
                return 3;
            if (n % 5 == 0)
                return 5;
            byte[] buffer = BitConverter.GetBytes(n);
            rand.NextBytes(buffer);
            long summand = BitConverter.ToInt64(buffer, 0);
            rand.NextBytes(buffer);
            long a = BitConverter.ToInt64(buffer, 0);
            long b = a;
            long divisor;
            //
            a = (n + a * a + summand) % n;
            b = (((n + b * b + summand) % n) * (n + (n + b * b + summand) % n) + summand) % n;
            divisor = GCD(a - b, n);

            while (divisor == 1) {
                a = (a * a + summand) % n;
                b = (((n + b * b + summand) % n) * (n + (n + b * b + summand) % n) + summand) % n;
                divisor = GCD(a - b, n);
            }
            return divisor;
        }

        private static long GCD(long a, long b) {
            return b == 0 ? a : GCD(b, (b + a) % b);
        }

        public static List<long> atkinSieve(int n) {
            if (n <= 1) {
                List<long> l = new List<long>();
                return l;
            }
            int sqrt = (int)(Math.Sqrt((double)n) + 1);
            bool[] primes = new bool[n]; //initialize list all false

            for (int i = 0; i < sqrt; i++) {
                for (int j = 0; j < sqrt; j++) {
                    int s = 4 * i * i + j * j;// 4i^2 +j^2
                    if (s < n && (s % 12 == 1 || s % 12 == 5)) {
                        primes[s] = !primes[s];
                    }
                    s = 3 * i * i + j * j;// 3i^2 +j^2
                    if (s < n && s % 12 == 7) {
                        primes[s] = !primes[s];
                    }
                    s = 3 * i * i - j * j; // 3i^2 - j^2
                    if (s < n && i > j && s % 12 == 11) {
                        primes[s] = !primes[s];
                    }
                }
            }

            List<long> result = new List<long>();
            result.Add(2L);
            result.Add(3L);
            long N = (long)n; //to prevent int overflow
            for (int i = 2; i < n; i++) {
                int j = 0;
                long I = (long)i;
                long J = (long)j;
                if (primes[i] == true) {
                    long K = I * I + I * J;
                    while (K < N && K <= Int32.MaxValue) {
                        primes[(int)K] = false;
                        j++;
                        J = (long)j;
                        K = I * I + I * J;
                    }
                    result.Add(I);
                }
            }
            return result;
        }

    }

    public static class Gamma {
        static int g = 7;
        static double[] p = {0.99999999999980993, 676.5203681218851, -1259.1392167224028,
	     771.32342877765313, -176.61502916214059, 12.507343278686905,
	     -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7};

        public static double GammaReal(double y) {
            y += 1;
            if (y < 0.5)
                return Math.PI / (Math.Sin(Math.PI * y) * GammaReal(1 - y));
            else {
                y -= 1;
                double x = p[0];
                for (var i = 1; i < g + 2; i++) {
                    x += p[i] / (y + i);
                }
                double t = y + g + 0.5;
                return Math.Sqrt(2 * Math.PI) * (Math.Pow(t, y + 0.5)) * Math.Exp(-t) * x;
            }
        }
    }

}