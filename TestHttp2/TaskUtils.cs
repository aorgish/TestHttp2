using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace TestHttp2
{
    public static class TaskUtils {

        public static IEnumerable<T> Throttle<T>(this IEnumerable<T> source, int buffSize) where T : Task {
            var buffer = new T[buffSize];
            var len = 0;
            foreach (var task in source) {
                if (len < buffSize) {
                    buffer[len++] = task;
                    continue;
                }
                var idx = Task.WaitAny(buffer);
                var completedTask = buffer[idx];
                buffer[idx] = task;
                if (!completedTask.IsFaulted) {
                    yield return completedTask;
                }
            }
            Task.WaitAll(buffer);
            foreach (var task in buffer.Where(t => !t.IsFaulted)) {
                yield return task;
            }
        }

        public static void Run<T>(this IEnumerable<T> pipeline) {
            foreach (var item in pipeline) { }
        }

        public static IEnumerable<string> GetAlexaTop1mlnUrls(string url) {
            var request = WebRequest.CreateHttp(url);
            using(var response = request.GetResponse())
            using(var responseStream = response.GetResponseStream())
            using(var archive = new ZipArchive(responseStream, ZipArchiveMode.Read))
            using(var stream = archive.Entries.First().Open())
            using(var reader = new StreamReader(stream)) {
                string line;
                while(!string.IsNullOrWhiteSpace(line = reader.ReadLine())) {
                    yield return line.Split(',')[1];
                }
            }
        }

    }
}
