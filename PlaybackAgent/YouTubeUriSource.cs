using Microsoft.Phone.BackgroundAudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Windows.Storage;

namespace PlayStation
{
    public enum YouTubeQuality
    {
        Quality480P,
        Quality720P,
        Quality1080P
    }

    public enum YouTubeThumbnailSize
    {
        Small,
        Medium,
        Large,
        MoviePoster
    }

    public class YouTubeQueryResponse
    {
        public string VideoId { get; set; }
        public string DurationSecs { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Description { get; set; }
        public List<string> Actors { get; set; }
        public string LastUpdated { get; set; }
        private AudioTrack _track;
        public static Action<YouTubeQueryResponse> QueueUpdate;
        private static Timer requestTimer;

        public bool IsAdded
        {
            get;
            set;
        }

        public bool InCache
        {
            get;
            set;
        }

        public string ResultUrl
        {
            get;
            set;
        }

        public string RequestUrl
        {
            get;
            set;
        }

        public YouTubeQueryResponse(string id, string durationSecs, string title, string thumbnailUrl, string description, List<string> actors, string lastUpdated)
        {
            VideoId = id;
            DurationSecs = durationSecs;
            Title = title;
            ThumbnailUrl = thumbnailUrl;
            Description = description;
            Actors = actors;
            LastUpdated = lastUpdated;
            RequestUrl = String.Format("http://www.youtube.com/get_video_info?&video_id={0}&el=detailpage&ps=default&eurl=&gl=US&hl=en", VideoId);
        }

        public AudioTrack GetTrack()
        {
            return _track;
        }

        public void GetUri(Action<Exception, YouTubeQueryResponse> callback)
        {
            string file = null;
            var taskRead = ReadFileCache();
            var actionTask = taskRead.AsAsyncAction();
            actionTask.Completed = new Windows.Foundation.AsyncActionCompletedHandler((action, status) =>
            {
                if (status == Windows.Foundation.AsyncStatus.Completed)
                {
                    file = taskRead.Result;
                    if (string.IsNullOrEmpty(file))
                    {

                        HttpWebRequest webRequest = HttpWebRequest.CreateHttp(RequestUrl);
                        webRequest.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; Win64; x64; Trident/6.0)";
                        var result = webRequest.BeginGetResponse(new AsyncCallback(obj =>
                        {
                            try
                            {
                                var webResponse = (HttpWebResponse)webRequest.EndGetResponse(obj);
                                if (webResponse.StatusCode == HttpStatusCode.OK)
                                {
                                    Stream responseStream = webResponse.GetResponseStream();
                                    using (StreamReader reader = new StreamReader(responseStream))
                                    {
                                        string readText = reader.ReadToEnd();
                                        var resultUrl = ExtractDownloadUrls(readText);
                                        if (resultUrl != null)
                                        {
                                            InCache = false;
                                            _track = new AudioTrack(new Uri(resultUrl, UriKind.Absolute), Title, null, null, new Uri(ThumbnailUrl));
                                            ResultUrl = resultUrl;
                                            callback(null, this);
                                        }

                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                callback(ex, this);
                            }
                        }), webRequest);
                    }
                    else
                    {
                        ResultUrl = file;
                        InCache = true;
                        _track = new AudioTrack(new Uri(ResultUrl, UriKind.Absolute), Title, null, null, new Uri(ThumbnailUrl));
                        callback(null, this);
                    }
                }
            });
        }

        private static string ExtractDownloadUrls(string source)
        {
            var urls = new List<Uri>();
            string exUrl = null;
            var list = ParseFormDecoded(source);
            foreach (var kv in list)
            {
                if (kv[0] != "url_encoded_fmt_stream_map") continue;
                var list2 = ParseFormDecoded(kv[1], ',');
                foreach (var kv2 in list2)
                {
                    var list3 = ParseFormDecoded(kv2[1]);
                    string url = "";
                    string fallback_host = "";
                    string sig = "";
                    foreach (var kv3 in list3)
                    {
                        switch (kv3[0])
                        {
                            case "url":
                                url = kv3[1];
                                break;
                            case "fallback_host":
                                fallback_host = kv3[1];
                                break;
                            case "sig":
                                sig = kv3[1];
                                break;
                        }
                    }
                    if (url.IndexOf("&fallback_host=", StringComparison.Ordinal) < 0)
                        url += "&fallback_host=" + WebUtility.UrlDecode(fallback_host);
                    if (url.IndexOf("&signature=", StringComparison.Ordinal) < 0)
                        url += "&signature=" + WebUtility.UrlDecode(sig);
                    urls.Add(new Uri(url));
                    if (url.IndexOf("itag=17") > 0)
                    {
                        exUrl = url;
                    }
                }
            }
            return exUrl;
        }

        private static List<string[]> ParseFormDecoded(string qs, char split = '&')
        {
            var arr = qs.Split(split);
            var list = new List<string[]>(arr.Length);
            foreach (var kv in arr)
            {
                if (split == ',')
                {
                    list.Add(new[] { "", kv });
                }
                else
                {
                    var akv = kv.Split('=');
                    var k = WebUtility.UrlDecode(akv[0]);
                    var v = WebUtility.UrlDecode(akv[1]);
                    list.Add(new[] { k, v });
                }
            }
            return list;
        }

        public void Download(Action<int> onCompleteCache)
        {
            HttpWebRequest webRequest = HttpWebRequest.CreateHttp(ResultUrl);
            webRequest.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; Win64; x64; Trident/6.0)";

            webRequest.BeginGetResponse(new AsyncCallback(obj =>
            {
                try
                {
                    DownloadFile(onCompleteCache, obj, webRequest);
                }
                catch
                {
                    onCompleteCache(1);
                }
            }), webRequest);
        }

        async void DownloadFile(Action<int> onCompleteCache, IAsyncResult obj, HttpWebRequest webRequest)
        {
            var webResponse = (HttpWebResponse)webRequest.EndGetResponse(obj);
            if (webResponse.StatusCode == HttpStatusCode.OK)
            {
                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(GetFileName(), CreationCollisionOption.ReplaceExisting);
                using (Stream s = await file.OpenStreamForWriteAsync())
                {
                    webResponse.GetResponseStream().CopyTo(s);
                    onCompleteCache(0);
                }
            }
        }

        private string GetFileName()
        {
            return string.Format("{0}.3gp", VideoId);
        }

        async Task<string> ReadFileCache()
        {
            try
            {
                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.GetFileAsync(GetFileName());
                return file.Path;
            }
            catch
            {
                return null;
            }
        }
    }

    public static class YouTubeClient
    {
        static XNamespace yt = "http://gdata.youtube.com/schemas/2007";
        static XNamespace gd = "http://schemas.google.com/g/2005";
        static XNamespace openSearch = "http://a9.com/-/spec/opensearch/1.1/";
        static XNamespace media = "http://search.yahoo.com/mrss/";
        static XNamespace xn = "http://www.w3.org/2005/Atom/";
        /*
        static string searchQueryString = "http://gdata.youtube.com/feeds/api/channels?q=IN_QUERY&start-index=1&v=2";
        static string genreChannelsString = "http://gdata.youtube.com/feeds/api/channelstandardfeeds/IN_COUNTRY/most_subscribed_IN_GENRE?v=2&start-index=1";
        static string popularChannelsString = "http://gdata.youtube.com/feeds/api/channelstandardfeeds/IN_COUNTRY/most_viewed?v=2&start-index=1";
        static string videosString = "http://gdata.youtube.com/feeds/api/videos?author=IN_AUTHOR&orderby=published&start-index=1&v=2";
         */
        static string musicChannelQuery = "http://gdata.youtube.com/feeds/api/videos?q=IN_QUERY&category=Music&safeSearch=moderate&orderby=viewCount&hl=en&v=2";

        public static Uri GetThumbnailUri(string youTubeId, YouTubeThumbnailSize size = YouTubeThumbnailSize.MoviePoster)
        {
            switch (size)
            {
                case YouTubeThumbnailSize.Small:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/default.jpg", UriKind.Absolute);
                case YouTubeThumbnailSize.Medium:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/hqdefault.jpg", UriKind.Absolute);
                case YouTubeThumbnailSize.Large:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/maxresdefault.jpg", UriKind.Absolute);
                case YouTubeThumbnailSize.MoviePoster:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/movieposter.jpg", UriKind.Absolute);
                default:
                    return null;
            }
            throw new Exception();
        }

        private static int GetQualityIdentifier(YouTubeQuality quality)
        {
            switch (quality)
            {
                case YouTubeQuality.Quality480P: return 18;
                case YouTubeQuality.Quality720P: return 22;
                case YouTubeQuality.Quality1080P: return 37;
            }
            throw new ArgumentException("maxQuality");
        }

        public static void QueryYoutubeVideos(string querySearch, Action<List<YouTubeQueryResponse>> queryResponse)
        {
            var videos = new List<YouTubeQueryResponse>();
            var query = musicChannelQuery.Replace("IN_QUERY", querySearch);

            #region "Might be used later"
            /*if (genre == YouTubeChannelGenre.MusicPlaylist)
                {
                    query = musicChannelQuery.Replace("IN_QUERY", id);
                }
                else
                {
                    query = videosString.Replace("IN_AUTHOR", id);
                    query = query.Replace("start-index=1", string.Format("start-index={0}", startIndex));
                }*/
            #endregion

            HttpWebRequest webRequest = HttpWebRequest.CreateHttp(query);
            webRequest.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; Win64; x64; Trident/6.0)";

            webRequest.BeginGetResponse(new AsyncCallback(obj =>
            {
                try
                {
                    var webResponse = (HttpWebResponse)webRequest.EndGetResponse(obj);
                    if (webResponse.StatusCode == HttpStatusCode.OK)
                    {
                        Stream responseStream = webResponse.GetResponseStream();
                        XDocument xdoc = XDocument.Load(responseStream, LoadOptions.None);
                        var properties = from node in xdoc.Descendants(media + "group")
                                         select new
                                         {
                                             VideoId = node.Element(yt + "videoid").Value,
                                             DurationSecs = node.Element(yt + "duration").FirstAttribute.Value,
                                             Title = node.Element(media + "title").Value,
                                             UploadedTime = node.Element(yt + "uploaded").Value,
                                             UrlList = node.Descendants(media + "thumbnail").Select(x => x.FirstAttribute.Value).ToList(),
                                         };



                        foreach (var property in properties)
                        {
                            string thumbNailurl;
                            if (property.UrlList.Count == 7)
                                thumbNailurl = property.UrlList[6];
                            else
                                thumbNailurl = property.UrlList[2];
                            videos.Add(new YouTubeQueryResponse(property.VideoId,
                                property.DurationSecs,
                                property.Title,
                                thumbNailurl,
                                null,
                                null,
                                property.UploadedTime));
                        }

                        queryResponse(videos);
                    }

                    queryResponse(null);
                }
                catch
                {
                    //never ever crash when the service is down or when we couldn't get the results
                    queryResponse(null);
                }

            }), webRequest);
        }
    }
}
