﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace BinStorage.TestApp
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2
                || !Directory.Exists(args[0])
                || !Directory.Exists(args[1]))
            {
                Console.WriteLine(@"Usage: Assignment.BinStorage.TestApp\bin\Debug\netcoreapp2.0\Assignment.BinStorage.TestApp.dll InputFolder StorageFolder");
                return;
            }

            // Create storage and add data
            Console.WriteLine("Creating storage from " + args[0]);
            Stopwatch sw = Stopwatch.StartNew();
            using (var storage = new BinaryStorage(new StorageConfiguration() { WorkingFolder = args[1] }))
            {
                try
                {
                    Directory.EnumerateFiles(args[0], "*", SearchOption.AllDirectories)
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
                    {
                        AddFile(storage, s);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);

                    storage.Rollback();

                    throw ex;
                }

                storage.Commit();
            }
            Console.WriteLine("Time to create: " + sw.Elapsed);

            // Open storage and read data
            Console.WriteLine("Verifying data");
            sw = Stopwatch.StartNew();
            using (var storage = new BinaryStorage(new StorageConfiguration() { WorkingFolder = args[1] }))
            {
                Directory.EnumerateFiles(args[0], "*", SearchOption.AllDirectories)
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
                    {
                        using (var resultStream = storage.Get(s))
                        {
                            using (var sourceStream = new FileStream(s, FileMode.Open, FileAccess.Read))
                            {
                                if (sourceStream.Length != resultStream.Length)
                                {
                                    throw new Exception(string.Format("Length did not match: Source - '{0}', Result - {1}", sourceStream.Length, resultStream.Length));
                                }

                                byte[] hash1, hash2;
                                using (MD5 md5 = MD5.Create())
                                {
                                    hash1 = md5.ComputeHash(sourceStream);

                                    md5.Initialize();
                                    hash2 = md5.ComputeHash(resultStream);
                                }

                                if (!hash1.SequenceEqual(hash2))
                                {
                                    throw new Exception(string.Format("Hashes do not match for file - '{0}'  ", s));
                                }
                            }
                        }
                    });
            }
            Console.WriteLine("Time to verify: " + sw.Elapsed);
            Console.ReadKey();
        }

        static void AddFile(IBinaryStorage storage, string fileName)
        {
            using (var file = new FileStream(fileName, FileMode.Open))
            {
                storage.Add(fileName, file);
            }
        }
    }
}
