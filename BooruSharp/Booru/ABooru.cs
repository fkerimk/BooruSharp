﻿using BooruSharp.Search;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace BooruSharp.Booru
{
    /// <summary>
    /// Defines basic capabilities of a booru. This class is <see langword="abstract"/>.
    /// </summary>
    public abstract partial class ABooru
    {
        /// <summary>
        /// Gets whether this booru is considered safe (that is, all posts on
        /// this booru have rating of <see cref="Search.Post.Rating.Safe"/>).
        /// </summary>
        public abstract bool IsSafe { get; }

        private protected virtual Search.Comment.SearchResult GetCommentSearchResult(object json)
            => throw new FeatureUnavailable();

        private protected virtual Search.Post.SearchResult GetPostSearchResult(JToken obj)
            => throw new FeatureUnavailable();

        private protected virtual Search.Post.SearchResult[] GetPostsSearchResult(object json)
            => throw new FeatureUnavailable();

        private protected virtual JToken ParseFirstPostSearchResult(object json)
            => throw new FeatureUnavailable();

        private protected virtual Search.Related.SearchResult GetRelatedSearchResult(object json)
            => throw new FeatureUnavailable();

        private protected virtual Search.Tag.SearchResult GetTagSearchResult(object json)
            => throw new FeatureUnavailable();

        private protected virtual Search.Wiki.SearchResult GetWikiSearchResult(object json)
            => throw new FeatureUnavailable();

        private protected virtual Search.Autocomplete.SearchResult[] GetAutocompleteResultAsync(object json)
            => throw new FeatureUnavailable();
        private protected virtual async Task<IEnumerable> GetTagEnumerableSearchResultAsync(Uri url)
        {
            if (TagsUseXml)
            {
                var xml = await GetXmlAsync(url);
                return xml.LastChild;
            }
            else
            {
                return JsonConvert.DeserializeObject<JArray>(await GetJsonAsync(url));
            }
        }

        /// <summary>
        /// Gets whether it is possible to search for related tags on this booru.
        /// </summary>
        public bool HasRelatedAPI => !_options.HasFlag(BooruOptions.NoRelated);

        /// <summary>
        /// Gets whether it is possible to search for wiki entries on this booru.
        /// </summary>
        public bool HasWikiAPI => !_options.HasFlag(BooruOptions.NoWiki);

        /// <summary>
        /// Gets whether it is possible to search for comments on this booru.
        /// </summary>
        public bool HasCommentAPI => !_options.HasFlag(BooruOptions.NoComment);

        /// <summary>
        /// Gets whether it is possible to search for tags by their IDs on this booru.
        /// </summary>
        public bool HasTagByIdAPI => !_options.HasFlag(BooruOptions.NoTagByID);

        /// <summary>
        /// Gets whether it is possible to search for the last comments on this booru.
        /// </summary>
        // As a failsafe also check for the availability of comment API.
        public bool HasSearchLastComment => HasCommentAPI && !_options.HasFlag(BooruOptions.NoLastComments);

        /// <summary>
        /// Gets whether it is possible to search for posts by their MD5 on this booru.
        /// </summary>
        public bool HasPostByMd5API => !_options.HasFlag(BooruOptions.NoPostByMD5);

        /// <summary>
        /// Gets whether it is possible to search for posts by their ID on this booru.
        /// </summary>
        public bool HasPostByIdAPI => !_options.HasFlag(BooruOptions.NoPostByID);

        /// <summary>
        /// Gets whether it is possible to get the total number of posts on this booru.
        /// </summary>
        public bool HasPostCountAPI => !_options.HasFlag(BooruOptions.NoPostCount);

        /// <summary>
        /// Gets whether it is possible to get multiple random images on this booru.
        /// </summary>
        public bool HasMultipleRandomAPI => !_options.HasFlag(BooruOptions.NoMultipleRandom);

        /// <summary>
        /// Gets whether this booru supports adding or removing favorite posts.
        /// </summary>
        public bool HasFavoriteAPI => !_options.HasFlag(BooruOptions.NoFavorite);

        /// <summary>
        /// Gets whether it is possible to autocomplete searches in this booru.
        /// </summary>
        public bool HasAutocompleteAPI => !_options.HasFlag(BooruOptions.NoAutocomplete);

        /// <summary>
        /// Gets whether this booru can't call post functions without search arguments.
        /// </summary>
        public bool NoEmptyPostSearch => _options.HasFlag(BooruOptions.NoEmptyPostSearch);

        /// <summary>
        /// Gets a value indicating whether searching by more than two tags at once is not allowed.
        /// </summary>
        public bool NoMoreThanTwoTags => _options.HasFlag(BooruOptions.NoMoreThan2Tags);

        /// <summary>
        /// Gets a value indicating whether http:// scheme is used instead of https://.
        /// </summary>
        protected bool UsesHttp => _options.HasFlag(BooruOptions.UseHttp);

        /// <summary>
        /// Gets a value indicating whether tags API uses XML instead of JSON.
        /// </summary>
        protected bool TagsUseXml => _options.HasFlag(BooruOptions.TagApiXml);

        /// <summary>
        /// Gets a value indicating whether comments API uses XML instead of JSON.
        /// </summary>
        protected bool CommentsUseXml => _options.HasFlag(BooruOptions.CommentApiXml);

        /// <summary>
        /// Gets a value indicating whether the max limit of posts per search is increased (used by Gelbooru).
        /// </summary>
        protected bool SearchIncreasedPostLimit => _options.HasFlag(BooruOptions.LimitOf20000);

        /// <summary>
        /// Checks for the booru availability.
        /// Throws <see cref="HttpRequestException"/> if service isn't available.
        /// </summary>
        public async Task CheckAvailabilityAsync()
        {
            await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, _imageUrl));
        }

        /// <summary>
        /// Add booru authentification to current request
        /// </summary>
        /// <param name="message">The request that is going to be sent</param>
        protected virtual void PreRequest(HttpRequestMessage message)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ABooru"/> class.
        /// </summary>
        /// <param name="domain">
        /// The fully qualified domain name. Example domain
        /// name should look like <c>www.google.com</c>.
        /// </param>
        /// <param name="format">The URL format to use.</param>
        /// <param name="options">
        /// The options to use. Use <c>|</c> (bitwise OR) operator to combine multiple options.
        /// </param>
        protected ABooru(string domain, UrlFormat format, BooruOptions options)
        {
            Auth = null;
            HttpClient = null;
            _options = options;

            bool useHttp = UsesHttp; // Cache returned value for faster access.
            BaseUrl = new Uri("http" + (useHttp ? "" : "s") + "://" + domain, UriKind.Absolute);
            _format = format;
            _imageUrl = CreateQueryString(format, format == UrlFormat.Philomena ? string.Empty : "post");

            if (_format == UrlFormat.IndexPhp)
                _imageUrlXml = new Uri(_imageUrl.AbsoluteUri.Replace("json=1", "json=0"));
            else if (_format == UrlFormat.PostIndexJson)
                _imageUrlXml = new Uri(_imageUrl.AbsoluteUri.Replace("index.json", "index.xml"));
            else
                _imageUrlXml = null;

            _tagUrl = CreateQueryString(format, "tag");

            if (HasWikiAPI)
                _wikiUrl = format == UrlFormat.Danbooru
                    ? CreateQueryString(format, "wiki_page")
                    : CreateQueryString(format, "wiki");

            if (HasRelatedAPI)
                _relatedUrl = format == UrlFormat.Danbooru
                    ? CreateQueryString(format, "related_tag")
                    : CreateQueryString(format, "tag", "related");

            if (HasCommentAPI)
                _commentUrl = CreateQueryString(format, "comment");

            if (HasAutocompleteAPI)
                _autocompleteUrl = format == UrlFormat.IndexPhp
                    ? new Uri(BaseUrl + "autocomplete.php")
                    : CreateQueryString(format, "autocomplete");
            switch (_format)
            {
                case UrlFormat.IndexPhp:
                    _autocompleteUrl = new Uri(BaseUrl + "autocomplete.php");
                    break;
                case UrlFormat.Danbooru:
                    _autocompleteUrl = new Uri(BaseUrl + "tags/autocomplete.json");
                    break;
                default:
                    _autocompleteUrl = CreateQueryString(_format, "autocomplete"); //this isn't supposed to work.
                    break;
            }
        }

        private Uri CreateQueryString(UrlFormat format, string query, string squery = "index") {
            
            string queryString;

            switch (format) {
                
                case UrlFormat.PostIndexJson:
                    queryString = query + "/" + squery + ".json";
                    break;

                case UrlFormat.IndexPhp:
                    queryString = "index.php?page=dapi&s=" + query + "&q=index&json=1";
                    break;

                case UrlFormat.Danbooru:
                    queryString = query == "related_tag" ? query + ".json" : query + "s.json";
                    break;

                case UrlFormat.Sankaku:
                    queryString = query == "wiki" ? query : query + "s";
                    break;

                case UrlFormat.Philomena:
                    queryString = $"api/v1/json/search/{query}{(string.IsNullOrEmpty(query) ? string.Empty : "s")}";
                    break;

                case UrlFormat.BooruOnRails:
                    queryString = $"api/v3/search/{query}s";
                    break;

                default: return BaseUrl;
            }

            return new Uri(BaseUrl + queryString);
        }

        // TODO: Handle limitrate
        private async Task<string> GetJsonAsync(string url) {
            
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var message = new HttpRequestMessage(HttpMethod.Get, url);
            message.Headers.UserAgent.ParseAdd("BooruSharp");
            
            PreRequest(message);
            var msg = await HttpClient.SendAsync(message);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (msg.StatusCode) {
                
                case (HttpStatusCode)422: throw new TooManyTags();
                case HttpStatusCode.Forbidden: throw new AuthentificationRequired(); // LINE 277
                default: msg.EnsureSuccessStatusCode();
                return await msg.Content.ReadAsStringAsync();
            }
        }

        private protected Task<string> GetJsonAsync(Uri url)
        {
            return GetJsonAsync(url.AbsoluteUri);
        }

        private async Task<XmlDocument> GetXmlAsync(string url)
        {
            var xmlDoc = new XmlDocument();
            var xmlString = await GetJsonAsync(url);
            // https://www.key-shortcut.com/en/all-html-entities/all-entities/
            xmlDoc.LoadXml(Regex.Replace(xmlString, "&([a-zA-Z]+);", HttpUtility.HtmlDecode("$1")));
            return xmlDoc;
        }

        private Task<XmlDocument> GetXmlAsync(Uri url)
        {
            return GetXmlAsync(url.AbsoluteUri);
        }

        private async Task<string> GetRandomIdAsync(string tags)  {
            
            var msg = await HttpClient.GetAsync(BaseUrl + "index.php?page=post&s=random&tags=" + tags);
            msg.EnsureSuccessStatusCode();
            return HttpUtility.ParseQueryString(msg.RequestMessage!.RequestUri!.Query).Get("id");
        }

        private static Uri CreateUrl(Uri url, params string[] args) {

            var delim = "&";
            
            if (url.ToString().Contains("danbooru.donmai.us")) delim = "$";
            
            
            
            var builder = new UriBuilder(url);
            builder.Query = builder.Query.Length > 1 ? string.Concat(builder.Query.AsSpan(1), delim, string.Join(delim, args)) : string.Join(delim, args);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(builder.Uri);
            Console.ResetColor();
            
            var uri = builder.Uri.ToString();
        
            if (uri.Contains("gelbooru.com"))
                uri = uri.Replace("&sort=random", "+sort:random");
            
            return new Uri(uri);
        }

        private string TagsToString(string[] tags) {
            
            if (tags == null || tags.Length == 0) {
                // Philomena doesn't support search with no tag so we search for all posts with ID > 0
                return _format == UrlFormat.Philomena || _format == UrlFormat.BooruOnRails ? "q=id.gte:0" : "tags=";
            }
            
            return (_format == UrlFormat.Philomena || _format == UrlFormat.BooruOnRails ? "q=" : "tags=")
                + string.Join(_format == UrlFormat.Philomena || _format == UrlFormat.BooruOnRails ? "," : "+", tags.Select(Uri.EscapeDataString)).ToLowerInvariant();
        }

        private string SearchArg(string value)
        {
            return _format == UrlFormat.Danbooru
                ? "search[" + value + "]="
                : value + "=";
        }

        /// <summary>
        /// Gets or sets authentication credentials.
        /// </summary>
        public BooruAuth Auth { set; get; }

        /// <summary>
        /// Sets the <see cref="System.Net.Http.HttpClient"/> instance that will be used
        /// to make requests. If <see langword="null"/> or left unset, the default
        /// <see cref="System.Net.Http.HttpClient"/> instance will be used.
        /// <para>This property can only be read in <see cref="ABooru"/> subclasses.</para>
        /// We advice you to disable the cookies and set automatic decompression to GZip and Deflate
        /// </summary>
        public HttpClient HttpClient
        {
            protected get
            {
                // If library consumers didn't provide their own client,
                // initialize and use singleton client instead.
                return _client ?? _lazyClient.Value;
            }
            set
            {
                _client = value;

                // Add our User-Agent if client's User-Agent header is empty.
                if (_client != null && !_client.DefaultRequestHeaders.Contains("User-Agent"))
                    _client.DefaultRequestHeaders.Add("User-Agent", _userAgentHeaderValue);
            }
        }

        /// <summary>
        /// Gets the instance of the thread-safe, pseudo-random number generator.
        /// </summary>
        protected static Random Random { get; } = new ThreadSafeRandom();

        /// <summary>
        /// Gets the base API request URL.
        /// </summary>
        public Uri BaseUrl { get; }

        private HttpClient _client;
        private readonly Uri _imageUrlXml, _imageUrl, _tagUrl, _wikiUrl, _relatedUrl, _commentUrl, _autocompleteUrl; // URLs for differents endpoints
        // All options are stored in a bit field and can be retrieved using related methods/properties.
        private readonly BooruOptions _options;
        private readonly UrlFormat _format; // URL format
        private const string _userAgentHeaderValue = "Mozilla/5.0 BooruSharp";
        private protected readonly DateTime _unixTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Lazy<HttpClient> _lazyClient = new Lazy<HttpClient>(() =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", _userAgentHeaderValue);
            return client;
        });
    }
}
