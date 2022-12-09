using Ionic.Zip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDownloader.Models;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly YoutubeClient youtubeClient;
        private Dictionary<string, Stream> dictStreams;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            youtubeClient = new YoutubeClient();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public async Task<IActionResult> Download(string video)
        {
            if (string.IsNullOrEmpty(video))
            {
                TempData["Erro"] = "Please input a url!";
                return RedirectToAction(nameof(Index));
            }

            if (video.Contains("list"))
            {
                return RedirectToAction(nameof(DownloadPlaylist), new { playlist = video });
            }

            if (video.Split(" ").Count() > 1)
            {
                return RedirectToAction(nameof(DownloadMany), new { urls = video.Split(" ") });
            }

            try
            {
                var download = await youtubeClient.Videos.GetAsync(video);

                var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(download.Id.Value);

                var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                var stream = await youtubeClient.Videos.Streams.GetAsync(streamInfo);

                return File(stream, "audio/mpeg", download.Title + ".mp3");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> DownloadMany(List<string> urls)
        {
            if (urls == null || urls.Count == 0)
            {
                TempData["Erro"] = "Please input a url!";
                return RedirectToAction(nameof(Index));
            }

            dictStreams = new Dictionary<string, Stream>();

            foreach (var url in urls)
            {
                if (url.Contains("list"))
                {
                    return RedirectToAction(nameof(DownloadPlaylist), new { playlist = url });
                }

                try
                {
                    var download = await youtubeClient.Videos.GetAsync(url);

                    var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(download.Id.Value);

                    var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    var stream = await youtubeClient.Videos.Streams.GetAsync(streamInfo);

                    dictStreams.Add(download.Title + ".mp3", stream);
                }
                catch (Exception ex)
                {
                    TempData["Erro"] = ex.Message;
                    return RedirectToAction(nameof(Index));
                }
            }

            using (var zip = new ZipFile())
            {
                foreach (var item in dictStreams)
                {
                    zip.AddEntry(item.Key, item.Value);
                }

                using (var memoryStream = new MemoryStream())
                {
                    zip.Save(memoryStream);

                    return File(memoryStream, "application/zip", "Downloads.zip");
                }
            }
        }

        public async Task<IActionResult> DownloadPlaylist(string playlist)
        {
            try
            {
                var downloads = await youtubeClient.Playlists.GetVideosAsync(playlist);
                var outputStream = new MemoryStream();

                List<Task> taskList = new List<Task>();

                dictStreams = new Dictionary<string, Stream>();

                using (ZipFile archive = new ZipFile())
                {
                    foreach (var download in downloads)
                    {
                        var task = DownloadMusica(download);

                        taskList.Add(task);
                    }

                    Task.WaitAll(taskList.ToArray());

                    foreach(var item in dictStreams)
                    {
                        if(item.Key != null && item.Value != null)
                            archive.AddEntry(item.Key, item.Value);
                    }

                    archive.Save(outputStream);
                }

                return File(outputStream.ToArray(), "application/zip", "Playlist.zip");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro: " + ex.Message + " - Exception: " + ex.StackTrace.ToString();
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task DownloadMusica(PlaylistVideo video)
        {
            try
            {
                var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(video.Id.Value);

                var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                if (!string.IsNullOrEmpty(video.Title))
                    dictStreams.Add($"{video.Title}.mp3", await youtubeClient.Videos.Streams.GetAsync(streamInfo));
            }
            catch
            {

            }
            
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
