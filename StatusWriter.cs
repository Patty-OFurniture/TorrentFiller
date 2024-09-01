using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// mostly from required bits of [TextWriter.cs]
// AppData\Local\SourceServer\ea3b335a130937bb698d8aea790fb76016997756d4c80da878a1b013436ae481\TextWriter.cs
namespace HashTester
{
    internal class StatusWriter : TextWriter
    {
        List<string> Lines = new List<string>();
        int current = 0;

        // compared to hosting console window
        private int? _xOffset = null;
        private int? _yOffset = null;
        // space to occupy in hosting console window
        private int? _Rows = null;
        private int? _Columns = null;

        private TextWriter? writer = null;
        private Encoding? encoding = null;

        public StatusWriter(int x = 0, int y = 0, int w = 0, int h = 0, TextWriter? writer = null)
        {
            if (x > 0)
                _xOffset = x;
            if (y > 0)
                _yOffset = y;
            if (w > 0)
                _Rows = w;
            if (h > 0)
                _Columns = h;

            if (writer != null)
            {
                this.writer = writer;
                encoding = writer.Encoding;
            }
        }

        internal void ReDraw()
        {
            foreach(var line in Lines)
            {
                Console.SetCursorPosition(_xOffset ?? 0, _yOffset ?? 0);
            }
        }

        public override Encoding Encoding => Encoding.UTF8;

        // Writes a character to the text stream. This default method is empty,
        // but descendant classes can override the method to provide the
        // appropriate functionality.
        //
        public override void Write(char value)
        {
            if (value == '\n')
            {
                Lines.Add("");
                current++;
                ReDraw();
            }
            else
            {
                Lines[current].Append(value);
            }
        }

        // Writes a character array to the text stream. This default method calls
        // Write(char) for each of the characters in the character array.
        // If the character array is null, nothing is written.
        //
        public override void Write(char[]? buffer)
        {
            // TODO: optimize
            // var nlbreaks = value.ToString().Split('\n');
            // System.Diagnostics.Debug.Assert(nlbreaks > 0);
            if (buffer != null)
            {
                Write(buffer, 0, buffer.Length);
            }
        }

        // Writes a range of a character array to the text stream. This method will
        // write count characters of data into this TextWriter from the
        // buffer character array starting at position index.
        //
        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer), "SR.ArgumentNull_Buffer");
            }
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "SR.ArgumentOutOfRange_NeedNonNegNum");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "SR.ArgumentOutOfRange_NeedNonNegNum");
            }
            if (buffer.Length - index < count)
            {
                throw new ArgumentException("SR.Argument_InvalidOffLen");
            }

            for (int i = 0; i < count; i++) 
                Write(buffer[index + i]);
        }

        public static StatusWriter Synchronized(StatusWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            return writer is SyncTextWriter ? writer : new SyncTextWriter(writer);
        }

        internal sealed class SyncTextWriter : StatusWriter, IDisposable
        {
            private readonly TextWriter _out;

            internal SyncTextWriter(StatusWriter t)
            {
                _out = t;
            }

            public override Encoding Encoding => _out.Encoding;

            public override IFormatProvider FormatProvider => _out.FormatProvider;

            [AllowNull]
            public override string NewLine
            {
                [MethodImpl(MethodImplOptions.Synchronized)]
                get => _out.NewLine;
                [MethodImpl(MethodImplOptions.Synchronized)]
                set => _out.NewLine = value;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Close() => _out.Close();

            [MethodImpl(MethodImplOptions.Synchronized)]
            protected override void Dispose(bool disposing)
            {
                // Explicitly pick up a potentially methodimpl'ed Dispose
                if (disposing)
                    ((IDisposable)_out).Dispose();
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Flush() => _out.Flush();

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(char value) => _out.Write(value);


            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(char[] buffer, int index, int count) => _out.Write(buffer, index, count);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(ReadOnlySpan<char> buffer) => _out.Write(buffer);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(bool value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(int value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(uint value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(long value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(ulong value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(float value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(double value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(decimal value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(string? value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(StringBuilder? value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(object? value) => _out.Write(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(string format, object? arg0) => _out.Write(format, arg0);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(string format, object? arg0, object? arg1) => _out.Write(format, arg0, arg1);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(string format, object? arg0, object? arg1, object? arg2) => _out.Write(format, arg0, arg1, arg2);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void Write(string format, object?[] arg) => _out.Write(format, arg);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine() => _out.WriteLine();

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(char value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(decimal value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(char[]? buffer) => _out.WriteLine(buffer);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(char[] buffer, int index, int count) => _out.WriteLine(buffer, index, count);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(ReadOnlySpan<char> buffer) => _out.WriteLine(buffer);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(bool value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(int value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(uint value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(long value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(ulong value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(float value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(double value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(string? value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(StringBuilder? value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(object? value) => _out.WriteLine(value);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(string format, object? arg0) => _out.WriteLine(format, arg0);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(string format, object? arg0, object? arg1) => _out.WriteLine(format, arg0, arg1);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(string format, object? arg0, object? arg1, object? arg2) => _out.WriteLine(format, arg0, arg1, arg2);

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override void WriteLine(string format, object?[] arg) => _out.WriteLine(format, arg);

            //
            // On SyncTextWriter all APIs should run synchronously, even the async ones.
            //

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override ValueTask DisposeAsync()
            {
                Dispose();
                return default;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteAsync(char value)
            {
                Write(value);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteAsync(string? value)
            {
                Write(value);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(cancellationToken);
                }

                Write(value);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteAsync(char[] buffer, int index, int count)
            {
                Write(buffer, index, count);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(cancellationToken);
                }

                Write(buffer.Span);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(cancellationToken);
                }

                WriteLine(buffer.Span);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteLineAsync(char value)
            {
                WriteLine(value);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteLineAsync()
            {
                WriteLine();
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteLineAsync(string? value)
            {
                WriteLine(value);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(cancellationToken);
                }

                WriteLine(value);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task WriteLineAsync(char[] buffer, int index, int count)
            {
                WriteLine(buffer, index, count);
                return Task.CompletedTask;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public override Task FlushAsync()
            {
                Flush();
                return Task.CompletedTask;
            }
        }

    }
}
