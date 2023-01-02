using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Drawing;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace AsciiArt
{
    public class Program
    {
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool WriteConsoleOutputW(SafeFileHandle hConsoleOutput, CharInfo[] lpBuffer, Coord dwBufferSize, Coord dwBufferCoord, ref SmallRect lpWriteRegion);

        [DllImport("Kernel32.dll")]
        public static extern SafeFileHandle GetStdHandle(int nStdHandle);

        //public static readonly CharInfo[] Chars = new CharInfo[]{ new CharInfo(){ Char =  '░' , Attributes = 7}, new CharInfo() { Char =  '▒', Attributes = 7 } , new CharInfo() { Char =  '▓', Attributes = 7 } };
        public static readonly char[] Chars = new char[] { '░', '▒', '▓' };

        public static SafeFileHandle ConsoleOutHandle;
        public static Coord BufferSize;
        public static Coord PointerCoord = new Coord() { x = 0, y = 0 };


        public static CharInfo[] ConsoleBuffer;

        public static void Main(string[] args)
        {
            //Setup console display buffer.

#if !DEBUG
            if (!args.Any())
                throw new ArgumentNullException(nameof(args), "An image needs to be provided.");

            string stringFilePath = args[0];

            if (string.IsNullOrEmpty(stringFilePath) || File.Exists(stringFilePath) == false)
                throw new FileNotFoundException(stringFilePath, "Invalid file path.");
#else

            string stringFilePath = "H:\\Big_Memer_Man.png";
#endif

            //Load the bitmap


            Console.WriteLine($"Ascii Art Generator. Path {stringFilePath}");

            Bitmap image = new Bitmap(stringFilePath, false);


            if (image == null)
                throw new NullReferenceException("Image failed to be loaded.");

            //Setup the console buffer
            Console.WriteLine($"Image Loaded Successfully. Type: {image.RawFormat} H: {image.Height} W: {image.Width}");

            Console.Write("Enter Pixels Per Row (How many pixels are condenced into one character): ");


            int pixelsPerCellRow = int.Parse(Console.ReadLine() ?? string.Empty);
            int halfRow = pixelsPerCellRow / 2;

            Console.Write("Enter Pixels Per Column (How many pixels are condenced into one character): ");

            int pixelsPerCellCol = int.Parse(Console.ReadLine() ?? string.Empty);
            int halfCol = pixelsPerCellCol / 2;

            int pixelsPerCell = pixelsPerCellCol * pixelsPerCellRow;


            Console.Write("Do you want to output to be inverted (looks better on discord)? y/N: ");

            bool inverted = Console.ReadKey().KeyChar == 'y';

            Console.WriteLine($"\n{pixelsPerCellRow} pixels per Row. {pixelsPerCellCol} pixels per Col.");

            int BufferHeight = image.Height / pixelsPerCellRow;
            int BufferWidth = image.Width / pixelsPerCellCol;
            CharInfo[] consoleBuffer = new CharInfo[BufferWidth * BufferHeight];

            Console.WriteLine($"Buffer Created Successfully H: {BufferHeight} W: {BufferWidth}");

            Console.CursorVisible = false;

            for (int i = 0; i < BufferWidth; i++)
            {
                for (int j = 0; j < BufferHeight; j++)
                {
                    //Calculate the brightness of the pixel

                    int currentW = i * pixelsPerCellCol + halfCol;
                    int currentH = j * pixelsPerCellRow + halfCol;

                    Color pixel = image.GetPixel(currentW, currentH);

                    int brightness = (pixel.R + pixel.G + pixel.B);
                    int adjustedBrightness = (brightness / 3);

                    int charTablePosition = adjustedBrightness / 85;

                    consoleBuffer[ConvertTo1DIndex(i, j, new Coord() { x = (short) BufferWidth, y = (short) BufferHeight})] = new CharInfo() { Char = Chars[(inverted == false ? (charTablePosition) : (2 - charTablePosition))], Attributes = 7  };
                }
            }

            short yPosition = (short) Console.CursorTop;
            
            ConsoleOutHandle = GetStdHandle(-11);

            DisplayBuffer(consoleBuffer, new Coord() { x = (short) BufferWidth, y = (short) BufferHeight}, new Coord() { x = 1, y = (short) (yPosition + 1)});

            Console.Title = "Press Enter To Exit.";
            Console.ReadLine();
        }

        public static void WriteToConsoleBuffer(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text), "Cannot have null text.");
            CharInfo[] textArray = InsertTextIntoBuffer(text);
            Coord size = new Coord() { x = 1, y = (short)textArray.Length };
            TransformBufferIntoMaster(textArray, PointerCoord, size);

            //Display the buffer.

            short y = PointerCoord.y;
            PointerCoord = new Coord { x = 0, y = 0 };
            DisplayBuffer(ConsoleBuffer, BufferSize, new Coord() { x = 0, y = 0});
        }

        public static void DisplayBuffer(CharInfo[] buffer, Coord size, Coord pos)
        {
            SafeFileHandle handle = GetStdHandle(-11);
            SmallRect rect = new SmallRect() { Bottom = (short) (size.y + pos.y), Left = pos.x, Right = (short) (size.x + pos.x), Top = pos.y };
            WriteConsoleOutputW(handle, buffer, size, pos, ref rect);
            handle.Dispose();
        }

        public static void TransformBufferIntoMaster(CharInfo[] charinfo, Coord position, Coord size)
        {
            if (charinfo == null || !withinBounds(position))
                throw new InvalidOperationException("Cannot use either null charinfo or outwith bounds.");


            for (short i = 0; i < size.y; i++)
            {
                for (short j = 0; j < size.x; j++)
                {
                    short currentX = (short)(i + position.x);
                    short currentY = (short)(j + position.y);

                    if (!withinBounds(currentX, currentY))
                        continue;

                    SetData(currentX, currentY, charinfo[ConvertTo1DIndex(i, j, size)]);
                }
            }

        }

        public static void SetData(Coord pos, CharInfo value) => SetData(pos.x, pos.y, value);

        public static CharInfo GetData(Coord pos) => GetData(pos.x, pos.y);

        public static void SetData(int x, int y, CharInfo value)
        {
            ConsoleBuffer[ConvertTo1DIndex(x, y, BufferSize)] = value;
        }

        public static CharInfo GetData(int x, int y)
        {
            return ConsoleBuffer[ConvertTo1DIndex(x, y, BufferSize)];
        }

        public static int ConvertTo1DIndex(int x, int y, Coord bufferSize)
        {
            return (bufferSize.x * y) + x;
        }

        public static bool withinBounds(Coord coord) => withinBounds(coord.x, coord.y);

        public static bool withinBounds(int x, int y) => (x > -1 && x < BufferSize.x && y > -1 && y < BufferSize.y);

        public static CharInfo[] InsertTextIntoBuffer(string textToInsert)
        {
            CharInfo[] newArray = new CharInfo[textToInsert.Length];
            for (int i = 0; i < textToInsert.Length; i++)
            {
                newArray[i].Char = textToInsert[i];
                newArray[i].Attributes = 1;
            }
            return newArray;
        }
    }



    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct CharInfo
    {
        public char Char;
        public short Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Coord
    {
        public short x;
        public short y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }
}