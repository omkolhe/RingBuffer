﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RingByteBuffer
{
    public class SequentialRingBuffer : RingBuffer
    {
        public SequentialRingBuffer(int maximumCapacity, byte[] buffer = null, bool allowOverwrite = false)
            : base(maximumCapacity, buffer, allowOverwrite)
        {
        }

        /// <inheritdoc />
        public override void Put(byte input)
        {
            if (ContentLength + 1 > Capacity) {
                if (CanOverwrite) {
                    Skip(1);
                } else {
                    throw new InvalidOperationException("Ringbuffer capacity insufficient for put/write operation.");
                }
            }

            Buffer[BufferTailOffset++] = input;
            if (BufferTailOffset == Capacity) {
                BufferTailOffset = 0;
            }
            ContentLength++;
        }

        /// <inheritdoc />
        public override void Put(byte[] buffer, int offset, int count)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offset must be positive.");
            }
            PutInitial(count);
            if (buffer.Length < offset + count) {
                throw new ArgumentException("Source array too small for requested input.");
            }

            while (count > 0) {
                int chunk = Math.Min(Capacity - BufferTailOffset, count);
                buffer.CopyBytes(offset, Buffer, BufferTailOffset, chunk);
                BufferTailOffset = (BufferTailOffset + chunk == Capacity) ? 0 : BufferTailOffset + chunk;
                ContentLength += chunk;
                offset += chunk;
                count -= chunk;
            }
        }

        /// <inheritdoc />
        public override int PutFrom(Stream source, int count)
        {
            PutInitial(count);

            int remaining = count;
            while (remaining > 0) {
                int chunk = Math.Min(Capacity - BufferTailOffset, remaining);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    var iterIn = source.Read(Buffer, BufferTailOffset, chunk - chunkIn);
                    if (iterIn < 1) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                BufferTailOffset = (BufferTailOffset + chunk == Capacity) ? 0 : BufferTailOffset + chunk;
                ContentLength += chunk;
                remaining -= chunk;
            }

            return count - remaining;
        }

        /// <inheritdoc />
        public override void PutExactlyFrom(Stream source, int count)
        {
            PutInitial(count);

            while (count > 0) {
                int chunk = Math.Min(Capacity - BufferTailOffset, count);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    var iterIn = source.Read(Buffer, BufferTailOffset, chunk - chunkIn);
                    if (iterIn < 1) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                BufferTailOffset = (BufferTailOffset + chunk == Capacity) ? 0 : BufferTailOffset + chunk;
                ContentLength += chunk;
                count -= chunk;
            }
        }

        /// <inheritdoc />
        public override async Task<int> PutFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            PutInitial(count);
            int remaining = count;
            while (remaining > 0) {
                int chunk = Math.Min(Capacity - BufferTailOffset, remaining);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    int iterIn = await source.ReadAsync(Buffer, BufferTailOffset, chunk - chunkIn, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) {
                        return count - remaining;
                    }
                    if (iterIn < 1) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                BufferTailOffset = (BufferTailOffset + chunk == Capacity) ? 0 : BufferTailOffset + chunk;
                ContentLength += chunk;
                remaining -= chunk;
            }

            return count - remaining;
        }

        /// <inheritdoc />
        public override async Task PutExactlyFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            PutInitial(count);
            while (count > 0) {
                int chunk = Math.Min(Capacity - BufferTailOffset, count);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    int iterIn = await source.ReadAsync(Buffer, BufferTailOffset, chunk - chunkIn, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) {
                        return;
                    }
                    if (iterIn < 1) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                BufferTailOffset = (BufferTailOffset + chunk == Capacity) ? 0 : BufferTailOffset + chunk;
                ContentLength += chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Verifies validity of <paramref name="count"/> parameter value.
        /// </summary>
        /// <param name="count">Number of bytes to put/write.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer has too much in it.</exception>
        protected void PutInitial(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (ContentLength + count > Capacity) {
                if (CanOverwrite) {
                    int skip = Capacity - (ContentLength + count);
                    Skip(skip);
                } else {
                    throw new ArgumentException("Ringbuffer capacity insufficient for put/write operation.", "count");
                }
            }
        }

        /// <inheritdoc />
        public override byte Take()
        {
            if (ContentLength == 0) {
                throw new InvalidOperationException("Ringbuffer contents insufficient for read operation.");
            }

            byte output = Buffer[BufferHeadOffset++];
            if (BufferHeadOffset == Capacity) {
                BufferHeadOffset = 0;
            }
            ContentLength--;

            return output;
        }

        /// <inheritdoc />
        public override void Take(byte[] buffer, int offset, int count)
        {
            TakeInitial(count);
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offsets must be positive.");
            }
            if (buffer.Length < offset + count) {
                throw new ArgumentException("Destination array too small for requested output.");
            }

            while (count > 0) {
                int chunk = Math.Min(Capacity - BufferHeadOffset, count);
                Buffer.CopyBytes(BufferHeadOffset, buffer, offset, chunk);
                BufferHeadOffset = (BufferHeadOffset + chunk == Capacity) ? 0 : BufferHeadOffset + chunk;
                ContentLength -= chunk;
                offset += chunk;
                count -= chunk;
            }
        }

        /// <inheritdoc />
        public override void TakeTo(Stream destination, int count)
        {
            TakeInitial(count);

            while (count > 0) {
                int chunk = Math.Min(Capacity - BufferHeadOffset, count);

                destination.Write(Buffer, BufferHeadOffset, chunk);
                BufferHeadOffset = (BufferHeadOffset + chunk == Capacity) ? 0 : BufferHeadOffset + chunk;
                ContentLength -= chunk;
                count -= chunk;
            }
        }

        /// <inheritdoc />
        public override async Task TakeToAsync(Stream destination, int count, CancellationToken cancellationToken)
        {
            TakeInitial(count);

            while (count > 0) {
                int chunk = Math.Min(Capacity - BufferHeadOffset, count);
                await destination.WriteAsync(Buffer, BufferHeadOffset, chunk, cancellationToken);
                if (cancellationToken.IsCancellationRequested) {
                    return;
                }
                BufferHeadOffset = (BufferHeadOffset + chunk == Capacity) ? 0 : BufferHeadOffset + chunk;
                ContentLength -= chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Verifies validity of <paramref name="count"/> parameter value.
        /// </summary>
        /// <param name="count">Number of bytes to take/read.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
        protected void TakeInitial(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (count > ContentLength) {
                throw new ArgumentException("Ringbuffer contents insufficient for take/read operation.", "count");
            }
        }
    }
}
