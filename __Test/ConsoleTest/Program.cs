// See https://aka.ms/new-console-template for more information
using Haley.Services;
using Haley.Enums;
using ConsoleTest;
using ConsoleTest.Models;
using Haley.Abstractions;
using Haley.Models;
using Haley.Utils;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Diagnostics;

//new Testing().ConfigTest();
await new Testing().StorageTest();

Console.ReadKey();


class Testing {
    public void ConfigTest() {
        var cfgTest = new ConfigTest();
        cfgTest.RegisterTest();

        bool flag = true;
        do {
            Console.WriteLine($@"Enter an option to proceed.");
            var key = Console.ReadKey();
            Console.WriteLine($@"{Environment.NewLine}");
            switch (key.Key) {
                case ConsoleKey.Escape:
                break;
                case ConsoleKey.A:
                Console.WriteLine(cfgTest.Cfg.GetConfig<ConfigOne>(false)?.ToJson());
                break;
                case ConsoleKey.B:
                Console.WriteLine(cfgTest.Cfg.GetConfig<ConfigTwo>(false)?.ToJson());
                break;
                case ConsoleKey.C:
                var original = cfgTest.Cfg.GetConfig<ConfigOne>(false);
                original.Price = 17500;
                Console.WriteLine(original?.ToJson());
                break;
                case ConsoleKey.D:
                var copy = cfgTest.Cfg.GetConfig<ConfigOne>();
                copy.Price = 93400;
                Console.WriteLine(copy?.ToJson());
                break;
                case ConsoleKey.E:
                break;
                case ConsoleKey.D1:
                cfgTest.Cfg.SaveAll().Wait(); //Save all
                break;
                case ConsoleKey.D2:
                cfgTest.SaveConfigTest().Wait(); //Save one by one
                break;
                case ConsoleKey.D3:
                cfgTest.Cfg.DeleteAllFiles();
                break;
                case ConsoleKey.D4:
                cfgTest.Cfg.DeleteFile<ConfigOne>();
                break;
                case ConsoleKey.D5:
                cfgTest.Cfg.ResetConfig<ConfigTwo>();
                break;
                case ConsoleKey.D6:
                cfgTest.Cfg.ResetAllConfig();
                break;
                case ConsoleKey.D7:
                cfgTest.Cfg.SaveAll(askProvider: false).Wait(); //Save all
                break;
                case ConsoleKey.D8:
                cfgTest.Cfg.LoadConfig<ConfigOne>();
                break;
                case ConsoleKey.D9:
                cfgTest.Cfg.LoadAllConfig();
                break;
                default:
                break;
            }
        } while (flag);

        //cfgTest.SaveConfigTest().Wait();
        //cfgTest.SaveAll().Wait();
        //cfgTest.DeleteAll();


        //IEnumerable<long> GetIds(int count = 5) {
        //    int i = count;
        //    while (i > 0) {
        //        yield return RandomUtils.GetBigInt(11);
        //        i--;
        //    }
        //}

        //foreach (var id in GetIds(12)) {
        //    Console.WriteLine(id);
        //}

        //var current = DateTime.UtcNow;
        //var ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current =current.AddYears(1);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current = current.AddYears(5);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current = current.AddYears(10);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current = current.AddYears(15);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current = current.AddYears(100);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current = current.AddYears(150);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");

        //current = current.AddYears(350);
        //ts = RandomUtils.GetTimeComponent(current);
        //Console.WriteLine($@"For TS : {current.ToLongDateString()}");
        //Console.WriteLine($@"Hours = {ts}");
    }
    public async Task StorageTest() {
        try {
            var sw = new Stopwatch();
            Console.WriteLine($@"Starting the service.");
            sw.Start();
            var _agw = new AdapterGateway() { ThrowCRUDExceptions = true }; //Only for testing.
            var dss = new DiskStorageService(_agw, "mss_db") { ThrowExceptions = true};
            await dss.RegisterClient(new OSSControlled("bcde"));
            await dss.RegisterClient(new OSSControlled("olacabs",OSSControlMode.Guid));
            await dss.RegisterClient("daep");
            await dss.RegisterModule("lingam","bcde");
            await dss.RegisterModule("bcde","daep");
            await dss.RegisterModule("arya","daep");
            await dss.RegisterWorkSpace("common", "daep","bcde");
            await dss.RegisterWorkSpace("demo2", "daep","bcde",OSSControlMode.Guid,OSSParseMode.Generate);
            await dss.RegisterModule(new OSSControlled("test",OSSControlMode.Guid),new OSSControlled("olacabs",OSSControlMode.Guid));
            await dss.RegisterModule(new OSSControlled("test12"),new OSSControlled("olacabs",OSSControlMode.Guid));
            await dss.RegisterModule(new OSSControlled("contest", OSSControlMode.Guid),new OSSControlled("bcde"));
            await dss.RegisterModule(new OSSControlled("test", OSSControlMode.Guid),new OSSControlled("bcde"));
            await dss.RegisterWorkSpace(null,"bcde","lingam");
            await dss.RegisterWorkSpace("demo2", "bcde", "lingam",OSSControlMode.None);
            sw.Stop();
            Console.WriteLine($@"Registered all modules & workspaces in {sw.Elapsed.TotalSeconds}");
          
            Console.WriteLine($@"Starting the File copy.");
            sw.Reset();
            sw.Start();
            //for (int i = 0; i < 4; i++) {
            //    var status = await dss.Upload(new OSSWriteRequest("daep","bcde") {
            //        FileStream = new FileStream(@"C:\Users\tmp168\Downloads\PNCL Data Compliance - Frame 1(4).jpg", FileMode.Open, FileAccess.Read),
            //        ResolveMode = OSSResolveMode.Revise,
            //        TargetName = @"C:\Users\tmp168\Downloads\response_1751620873480.jpg",
            //    }.SetComponent(new OSSControlled("common",isVirtual:true),OSSComponent.WorkSpace));
            //    Console.WriteLine($@"Status : {status.Status}, Message : {status.Message}");
            //}

            var dirpath = @"C:\Users\tmp168\Pictures";

            if (Directory.Exists(dirpath)) {
                foreach (var file in Directory.GetFiles(dirpath)) {
                    var status = await dss.Upload((IOSSWrite)new OSSWriteRequest("bcde", "lingam") {
                        FileStream = new FileStream(file, FileMode.Open, FileAccess.Read),
                        ResolveMode = OSSResolveMode.Revise
                    }.SetFile(new OSSFileRoute() { Cuid = "24a7ae50-7766-11f0-ac34-1860248785f1" }));
                    //}
                    //.SetFolder(new OSSFolderRoute() { Cuid = "75d20ac5-75e4-11f0-ac34-1860248785f1" }));
                    Console.WriteLine($@"{Environment.NewLine}Status : {status.Status}, Message : {status.Message}, Result : {status.Result?.ToJson() ?? string.Empty}");
                    //break;
                }
            }

            //var dld = await dss.Download((IOSSReadFile)new OSSReadFile("daep", "bcde")
            //    .SetFile(new OSSFileRoute() { Cuid = "39f5288f-75d2-11f0-ac34-1860248785f1" }));
            //.SetFile(new OSSFileRoute() { Id = 2107 })
            //.SetFile(new OSSFileRoute() { Name = "avtr_girl_01.jpg" })
            //.SetComponent(new OSSControlled("demo2", isVirtual: true), OSSComponent.WorkSpace));
            //.SetComponent(new OSSControlled("demo2", isVirtual: true), OSSComponent.WorkSpace)
            //.SetTargetName("airport_v.jpg"));

            //var dld = await dss.Download((IOSSReadFile)new OSSReadFile("daep", "bcde")
            //    .SetComponent(new OSSControlled("demo2", isVirtual: true), OSSComponent.WorkSpace)
            //    .SetTargetName("airport_v.jpg"));
            sw.Stop();
            Console.WriteLine($@"File upload completed. {sw.Elapsed.TotalSeconds}");
            Console.ReadKey();

        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
        }
    }
}