// <copyright file="DefaultRandomGenerator.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>

using System.Runtime.CompilerServices;

namespace Jigen.Indexer
{
    public sealed class DefaultRandomGenerator : IProvideRandomValues
    {
        /// <summary>
        /// This is the default configuration (it supports the optimization process to be executed on multiple threads)
        /// </summary>
        public static DefaultRandomGenerator Instance { get; } = new DefaultRandomGenerator(allowParallel: true);

        /// <summary>
        /// This uses the same random number generator but forces the optimization process to run on a single thread (which may be desirable if multiple requests may be processed concurrently
        /// or if it is otherwise not desirable to let a single request access all of the CPUs)
        /// </summary>
        public static DefaultRandomGenerator DisableThreading { get; } = new DefaultRandomGenerator(allowParallel: false);

        private DefaultRandomGenerator(bool allowParallel) => IsThreadSafe = allowParallel;

        public bool IsThreadSafe { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Next(int minValue, int maxValue) => ThreadSafeFastRandom.Next(minValue, maxValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextFloat() => ThreadSafeFastRandom.NextFloat();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NextFloats(Span<float> buffer) => ThreadSafeFastRandom.NextFloats(buffer);
    }
}
