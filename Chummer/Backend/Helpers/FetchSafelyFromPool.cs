/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using Microsoft.Extensions.ObjectPool;

namespace Chummer
{
    /// <summary>
    /// Legacy compatibility alias for older plugin code.
    /// </summary>
    public readonly struct FetchSafelyFromPool<T> : IDisposable, IEquatable<FetchSafelyFromPool<T>> where T : class
    {
        private readonly FetchSafelyFromObjectPool<T> _inner;

        [CLSCompliant(false)]
        public FetchSafelyFromPool(ObjectPool<T> objMyPool, out T objReturn)
        {
            _inner = new FetchSafelyFromObjectPool<T>(objMyPool, out objReturn);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public override bool Equals(object obj)
        {
            return obj is FetchSafelyFromPool<T> other && Equals(other);
        }

        public bool Equals(FetchSafelyFromPool<T> other)
        {
            return _inner.Equals(other._inner);
        }

        public override int GetHashCode()
        {
            return _inner.GetHashCode();
        }

        public static bool operator ==(FetchSafelyFromPool<T> left, FetchSafelyFromPool<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FetchSafelyFromPool<T> left, FetchSafelyFromPool<T> right)
        {
            return !(left == right);
        }
    }
}
