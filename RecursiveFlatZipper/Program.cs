using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.RegularExpressions;

namespace RecursiveFlatZipper
{
    class Options {

        [Option('o',"output",Required =true,HelpText ="Output zip file")]
        public string outputFile { get; set; } = "";
        [Option('r',"regex",Required =true,HelpText ="Regular expression to check whether a file should be included")]
        public string searchRegexString { get; set; } = "";
        [Option('p',"path",Required =false,Default =".",HelpText ="Root folder for searching")]
        public string rootFolder { get; set; } = ".";
    }


    class Program
    {
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsed(Run).WithNotParsed(ParseError);
        }

        static void ParseError(IEnumerable<Error> errors)
        {
#if DEBUG
            Console.ReadKey();
#endif
        }

        static void Run(Options opts)
        {
            Regex regex = new Regex(opts.searchRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Dictionary<string,string> filesToAdd = new Dictionary<string, string>();
            ProcessFolder(ref filesToAdd,opts.rootFolder, regex);

            if(filesToAdd.Count == 0)
            {
                Console.WriteLine("No files found.");
#if DEBUG
                Console.ReadKey();
#endif
                return;
            }

            using (FileStream fs = new FileStream(opts.outputFile, FileMode.CreateNew))
            {

                using (ZipArchive archive = new ZipArchive(fs,ZipArchiveMode.Create,true))
                {

                    foreach(var kvp in filesToAdd)
                    {
                        Console.Write($"Compressing {kvp.Value} as {kvp.Key} ... ");

                        archive.CreateEntryFromFile(kvp.Value, kvp.Key);
                        Console.WriteLine("done.");
                    }
                }
            }
            Console.WriteLine("All done.");
#if DEBUG
            Console.ReadKey();
#endif 
            return;
        }

        static void ProcessFolder(ref Dictionary<string, string> filesToAdd,string path, Regex matchPattern)
        {
            string[] files = Directory.GetFiles(path);
            string[] folders = Directory.GetDirectories(path);

            foreach(string file in files)
            {
                string fullRealFilename = Path.GetFileName(file);
                if (!matchPattern.Match(fullRealFilename).Success)
                {
                    continue;
                }
                string filename = Path.GetFileNameWithoutExtension(file);
                string ext = Path.GetExtension(file);
                int index = 1;
                string theFilename = filename + ext;
                bool duplicateOfExistingFile = false;
                while (filesToAdd.ContainsKey(theFilename))
                {
                    if (FilesAreEqualVectorized(new FileInfo(filesToAdd[theFilename]),new FileInfo(file))) // Avoid duplicating files
                    {
                        Console.WriteLine($"{file} is a duplicate of {filesToAdd[theFilename]}, not compressing.");
                        duplicateOfExistingFile = true;
                        break;
                    }
                    index++;
                    theFilename = filename + $"_{index}" + ext;
                }
                if (!duplicateOfExistingFile)
                {
                    filesToAdd[theFilename] = file;
                    Console.WriteLine($"Queueing {file} as {theFilename}.");
                }
            }
            foreach(string folder in folders)
            {
                ProcessFolder(ref filesToAdd, folder, matchPattern);
            }
        }


        // Following file comparison code taken from: https://stackoverflow.com/a/1359947
        const int BYTES_TO_READ = sizeof(Int64);

        static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                        return false;
                }
            }

            return true;
        }

        static int BYTES_TO_READ_COUNT = Vector<Int64>.Count;
        static int BYTES_TO_READ_TOTAL = BYTES_TO_READ * BYTES_TO_READ_COUNT;

        static bool FilesAreEqualVectorized(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ_TOTAL);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ_TOTAL];
                byte[] two = new byte[BYTES_TO_READ_TOTAL];

                Vector<Int64> oneV, twoV;

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ_TOTAL);
                    fs2.Read(two, 0, BYTES_TO_READ_TOTAL);

                    oneV = new Vector<long>(one);
                    twoV = new Vector<long>(two);
                    if (oneV != twoV)
                        return false;
                }
            }

            return true;
        }
    }
}
