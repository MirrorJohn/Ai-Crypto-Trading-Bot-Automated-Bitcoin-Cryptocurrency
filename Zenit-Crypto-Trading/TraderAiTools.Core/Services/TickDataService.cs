using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;

namespace TraderTools.Core.Services
{
    [Export(typeof(TickDataService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TickDataService
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IDataDirectoryService _dataDirectoryService;

        [ImportingConstructor]
        public TickDataService(IDataDirectoryService dataDirectoryService)
        {
            _dataDirectoryService = dataDirectoryService;
        }

        private void TrySaveData(List<(bool Completed, int TickDataItemsCount)> completedIndexes, List<TickData> tickDataItems, string path, ref int saveUptoIndex)
        {
            var newSaveUptoIndex = -1;
            var tickDataItemsCount = 0;
            for (var i = 0; i < completedIndexes.Count; i++)
            {
                if (completedIndexes[i].Completed)
                {
                    newSaveUptoIndex = i;
                    tickDataItemsCount = completedIndexes[i].TickDataItemsCount;
                }
                else
                {
                    break;
                }
            }

            if (newSaveUptoIndex == -1 || newSaveUptoIndex <= saveUptoIndex)
            {
                return;
            }

            SaveData(tickDataItems.Take(tickDataItemsCount).ToList(), path);
            saveUptoIndex = newSaveUptoIndex;
        }

        public static void SaveData(List<TickData> tickDataItems, string path)
        {
            Log.Info("Saving data");
            var added = 0;

            using (var memoryStream = new MemoryStream())
            using (var compressionStream = new GZipStream(memoryStream, CompressionMode.Compress)) // Compresses tick data to 18% of original size
            {
                TickData? lastAdded = null;
                foreach (var tickData in tickDataItems.OrderBy(x => x.Datetime).ThenByDescending(x => x.High - x.Low))
                {
                    if (lastAdded == null || tickData.Datetime != lastAdded.Value.Datetime)
                    {
                        var bytes = TickData.GetBytesFast(tickData);
                        compressionStream.Write(bytes, 0, bytes.Length);
                        lastAdded = tickData;
                        added++;
                    }
                    else if (lastAdded != null &&
                             (lastAdded.Value.Open != tickData.Open
                              || lastAdded.Value.Close != tickData.Close
                              || lastAdded.Value.High != tickData.High
                              || lastAdded.Value.Close != tickData.Close))
                    {
                        //Debugger.Break();
                    }
                    else
                    {

                    }
                }

                compressionStream.Close();

                File.WriteAllBytes(path, memoryStream.ToArray());
            }

            Log.Info("Data saved");
        }

        /*public List<TickData> GetTickData(string market, IBroker broker, bool updateData)
        {
            Log.Info($"Getting tick data for {market}");
            var path = Path.Combine(_dataDirectoryService.MainDirectory, "TickData", $"{market.Replace("/", "")}.dat");

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            var start = new DateTime(2017, 1, 1);
            var end = DateTime.UtcNow;

            // Load bytes from file
            List<TickData> tickDataItems = null;
            if (File.Exists(path))
            {
                Log.Info("Loading existing data from disk");

                var compressedTickData = File.ReadAllBytes(path);

                Log.Info("Loaded data from disk - now deserialising");
                tickDataItems = Deserialize(new MemoryStream(compressedTickData));
                Log.Info("Data deserialised");
            }

            if (updateData)
            {
                Log.Info($"Getting tick data for {market} - parallel");
                var tickStart = Environment.TickCount;
                
                if (tickDataItems != null)
                {
                    end = tickDataItems[0].Datetime.AddSeconds(1);

                    // Download newest data include some of the newest stored data as recent tick data does seem to change at times
                    DownloadData(market, broker, tickDataItems, path, tickDataItems[tickDataItems.Count - 1].Datetime.AddMinutes(-30), end);
                }
                else
                {
                    tickDataItems = new List<TickData>();
                }

                // Download everything else
                DownloadData(market, broker, tickDataItems, path, start, end);

                var time = Environment.TickCount - tickStart;
                Log.Info($"Got tick data (parallel) in {time}ms");
            }

            var ret = new List<TickData>();
            TickData? lastAdded = null;
            foreach (var tickData in tickDataItems.OrderBy(x => x.Datetime))
            {
                if (lastAdded == null || tickData.Datetime != lastAdded.Value.Datetime)
                {
                    ret.Add(tickData);
                    lastAdded = tickData;
                }
            }

            Log.Info("Got tick data");

            return ret;
        }*/

        /*private void DownloadData(string market, IBroker broker, List<TickData> tickDataItems, string path, DateTime start, DateTime end)
        {
            var saveUptoIndex = -1;
            var completed = new List<(bool Completed, int TickDataItemsCount)>();
            var producerConsumer = new ProducerConsumer<(DateTime Start, DateTime End, int CompletedIndex)>(3, d =>
            {
                var partial = broker.GetTickData(broker, market, d.Start, d.End);
                lock (tickDataItems)
                {
                    tickDataItems.AddRange(partial);
                    var newItems = tickDataItems.OrderByDescending(x => x.Datetime).ToList();
                    tickDataItems.Clear();
                    tickDataItems.AddRange(newItems);
                    completed[d.CompletedIndex] = (true, tickDataItems.Count);
                    TrySaveData(completed, tickDataItems, path, ref saveUptoIndex);
                }

                return ProducerConsumerActionResult.Success;
            });


            var queue = new List<(DateTime Start, DateTime End, int CompletedIndex)>();
            var e = end;
            while (e > start)
            {
                var s = e.AddHours(-6);
                if (s < start)
                {
                    s = start;
                }

                completed.Add((false, 0));
                queue.Add((s, e.AddSeconds(1), completed.Count - 1));

                e = s;
            }

            producerConsumer.Add(queue);

            producerConsumer.Start();
            producerConsumer.SetProducerCompleted();
            producerConsumer.WaitUntilConsumersFinished();
            TrySaveData(completed, tickDataItems, path, ref saveUptoIndex);
        }*/

        public static List<TickData> Deserialize(Stream stream)
        {
            using (var zipStream = new GZipStream(stream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                var ret = new List<TickData>();

                resultStream.Position = 0;
                while (resultStream.Position != resultStream.Length)
                {
                    var tickData = TickData.GetTickData(resultStream);

                    ret.Add(tickData);
                }

                return ret;
                
            }
        }
    }
}
