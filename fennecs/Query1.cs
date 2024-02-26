﻿// SPDX-License-Identifier: MIT

using fennecs.pools;

namespace fennecs;

/// <summary>
/// <para>
/// Query with 1 output Stream Type, <c>C0</c>.
/// </para>
/// <para>
/// Queries expose methods to rapidly iterate all Entities that match their Mask and Stream Types.
/// </para>
/// <ul>
/// <li><c>ForEach(...)</c> - call a delegate <see cref="RefAction{C0}"/> for each Entity.</li>
/// <li><c>Job(...)</c> - parallel process, calling a delegate <see cref="RefAction{C0}"/> for each Entity.</li>
/// <li><c>Raw(...)</c> - pass Memory regions / Spans too a delegate <see cref="MemoryAction{C0}"/> per matched Archetype (× matched Wildcards) of entities.</li>
/// </ul>
/// </summary>
/// <remarks>
/// 
/// </remarks>
public class Query<C0> : Query
{
    #region Internals

    /// <summary>
    /// Initializes a new instance of the <see cref="Query{C0}"/> class.
    /// </summary>
    /// <param name="world">The world context for the query.</param>
    /// <param name="streamTypes">The stream types for the query.</param>
    /// <param name="mask">The mask for the query.</param>
    /// <param name="archetypes">The archetypes for the query.</param>
    internal Query(World world, List<TypeExpression> streamTypes, Mask mask, List<Archetype> archetypes) : base(world, streamTypes, mask, archetypes)
    {
    }

    #endregion


    #region Runners

    /// <summary>
    /// Executes an action for each entity that match the query.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void ForEach(RefAction<C0> action)
    {
        AssertNotDisposed();

        using var worldLock = World.Lock;
        foreach (var table in Archetypes)
        {
            var count = table.Count;

            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var span0 = s0.AsSpan(0, count);
                foreach (ref var c0 in span0) action(ref c0);
            } while (join.Iterate());
        }
    }


    /// <summary>
    /// Executes an action for each entity that match the query, passing an additional uniform parameter to the action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="uniform">The uniform parameter to pass to the action.</param>
    public void ForEach<U>(RefActionU<C0, U> action, U uniform)
    {
        AssertNotDisposed();

        using var worldLock = World.Lock;

        foreach (var table in Archetypes)
        {
            var count = table.Count;

            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var span0 = s0.AsSpan(0, count);
                foreach (ref var c0 in span0)
                {
                    action(ref c0, uniform);
                }
            } while (join.Iterate());
        }
    }


    /// <summary>
    /// Executes an action for each entity that match the query in parallel.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="chunkSize">The size of the chunk for parallel processing.</param>
    public void Parallel(RefAction<C0> action, int chunkSize = int.MaxValue)
    {
        AssertNotDisposed();

        using var worldLock = World.Lock;
        Countdown.Reset();

        using var jobs = PooledList<Work<C0>>.Rent();

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            var count = table.Count; // storage.Length is the capacity, not the count.
            var partitions = count / chunkSize + Math.Sign(count % chunkSize);
            do
            {
                for (var chunk = 0; chunk < partitions; chunk++)
                {
                    Countdown.AddCount();

                    var start = chunk * chunkSize;
                    var length = Math.Min(chunkSize, count - start);

                    var s0 = join.Select;

                    var job = JobPool<Work<C0>>.Rent();
                    job.Memory1 = s0.AsMemory(start, length);
                    job.Action = action;
                    job.CountDown = Countdown;
                    jobs.Add(job);

                    ThreadPool.UnsafeQueueUserWorkItem(job, true);
                }
            } while (join.Iterate());
        }

        Countdown.Signal();
        Countdown.Wait();

        JobPool<Work<C0>>.Return(jobs);
    }


    /// <summary>
    /// Executes an action for each entity that match the query in parallel, passing an additional uniform parameter to the action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="uniform">The uniform parameter to pass to the action.</param>
    /// <param name="chunkSize">The size of the chunk for parallel processing.</param>
    public void Parallel<U>(RefActionU<C0, U> action, U uniform, int chunkSize = int.MaxValue)
    {
        AssertNotDisposed();

        using var worldLock = World.Lock;
        Countdown.Reset();

        using var jobs = PooledList<UniformWork<C0, U>>.Rent();

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);


            var count = table.Count; // storage.Length is the capacity, not the count.
            var partitions = count / chunkSize + Math.Sign(count % chunkSize);
            do
            {
                for (var chunk = 0; chunk < partitions; chunk++)
                {
                    Countdown.AddCount();

                    var start = chunk * chunkSize;
                    var length = Math.Min(chunkSize, count - start);

                    var s0 = join.Select;

                    var job = JobPool<UniformWork<C0, U>>.Rent();
                    job.Memory1 = s0.AsMemory(start, length);
                    job.Action = action;
                    job.Uniform = uniform;
                    job.CountDown = Countdown;
                    jobs.Add(job);

                    ThreadPool.UnsafeQueueUserWorkItem(job, true);
                }
            } while (join.Iterate());
        }

        Countdown.Signal();
        Countdown.Wait();

        JobPool<UniformWork<C0, U>>.Return(jobs);
    }


    /// <summary>
    /// Executes an action for each memory object that match the query.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Raw(MemoryAction<C0> action)
    {
        AssertNotDisposed();

        using var worldLock = World.Lock;

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var mem0 = s0.AsMemory(0, table.Count);
                action(mem0);
            } while (join.Iterate());
        }
    }


    /// <summary>
    /// Executes an action for each memory object that match the query, passing an additional uniform parameter to the action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="uniform">The uniform parameter to pass to the action.</param>
    public void Raw<U>(MemoryActionU<C0, U> action, U uniform)
    {
        AssertNotDisposed();

        using var worldLock = World.Lock;

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var mem0 = s0.AsMemory(0, table.Count);
                action(mem0, uniform);
            } while (join.Iterate());
        }
    }

    #endregion
}