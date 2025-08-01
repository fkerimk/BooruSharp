﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BooruSharp.Booru
{
    public abstract partial class ABooru
    {
        /// <summary>
        /// Gets the tags related to the specified <paramref name="tag"/>.
        /// </summary>
        /// <param name="tag">The tag that other tags must be related to.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="Search.FeatureUnavailable"/>
        /// <exception cref="System.Net.Http.HttpRequestException"/>
        public async Task<Search.Related.SearchResult[]> GetRelatedAsync(string tag) {
            
            if (!HasRelatedAPI) throw new Search.FeatureUnavailable();

            ArgumentNullException.ThrowIfNull(tag);

            var isDanbooruFormat = _format == UrlFormat.Danbooru;

            var content = JsonConvert.DeserializeObject<JObject>(await GetJsonAsync(CreateUrl(_relatedUrl, (isDanbooruFormat ? "query" : "tags") + "=" + tag)));

            var jsonArray = (JArray)(isDanbooruFormat
                ? content["tags"]
                : content[content.Properties().First().Name]);

            return jsonArray!.Select(GetRelatedSearchResult).ToArray();
        }
    }
}
