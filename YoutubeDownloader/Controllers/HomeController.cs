using Ionic.Zip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YoutubeDownloader.Models;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly YoutubeClient youtubeClient;

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

            if (video.Contains("playlist"))
            {
                return RedirectToAction(nameof(DownloadPlaylist), new { playlist = video });
            }

            var download = await youtubeClient.Videos.GetAsync(video);

            var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(download.Id.Value);

            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            var stream = await youtubeClient.Videos.Streams.GetAsync(streamInfo);

            return File(stream, "audio/mpeg", download.Title + ".mp3");
        }

        public async Task<IActionResult> DownloadPlaylist(string playlist)
        {
            var downloads = await youtubeClient.Playlists.GetVideosAsync(playlist);
            var outputStream = new MemoryStream();

            using (ZipFile archive = new ZipFile())
            {
                foreach (var download in downloads)
                {
                    var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(download.Id.Value);

                    var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    var stream = await youtubeClient.Videos.Streams.GetAsync(streamInfo);

                    archive.AddEntry(download.Title + ".mp3", stream);
                }

                archive.Save(outputStream);
            }

            return File(outputStream.ToArray(), "application/zip", "Playlist.zip");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
