﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BooruSharp.Booru;

public abstract partial class ABooru {
    
    private const int LimitedTagsSearchCount = 2;
    private const int IncreasedPostLimitCount = 20001;

    private string GetLimit(int quantity) => (_format is UrlFormat.Philomena or UrlFormat.BooruOnRails ? "per_page=" : "limit=") + quantity;

    /// <summary>
    /// Searches for a post using its MD5 hash.
    /// </summary>
    /// <param name="md5">The MD5 hash of the post to search.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="Search.FeatureUnavailable"/>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    public virtual async Task<Search.Post.SearchResult> GetPostByMd5Async(string md5) {
        
        if (!HasPostByMd5API) throw new Search.FeatureUnavailable();

        ArgumentNullException.ThrowIfNull(md5);

        return await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), "md5=" + md5));
    }

    /// <summary>
    /// Searches for a post using its ID.
    /// </summary>
    /// <param name="id">The ID of the post to search.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="Search.FeatureUnavailable"/>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    public virtual async Task<Search.Post.SearchResult> GetPostByIdAsync(int id) {
        
        if (!HasPostByIdAPI) throw new Search.FeatureUnavailable();

        return _format switch {
            
            UrlFormat.Danbooru => await GetSearchResultFromUrlAsync(BaseUrl + "posts/" + id + ".json"),
            UrlFormat.Philomena => await GetSearchResultFromUrlAsync($"{BaseUrl}api/v1/json/images/{id}"),
            UrlFormat.BooruOnRails => await GetSearchResultFromUrlAsync($"{BaseUrl}api/v3/posts/{id}"),
            UrlFormat.PostIndexJson => await GetSearchResultFromUrlAsync(_imageUrl + "?tags=id:" + id),
            
            _ => await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), "id=" + id))
        };
    }

    /// <summary>
    /// Gets the total number of available posts. If <paramref name="tagsArg"/> array is specified
    /// and isn't empty, the total number of posts containing these tags will be returned.
    /// </summary>
    /// <param name="tagsArg">The optional array of tags.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="Search.FeatureUnavailable"/>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    /// <exception cref="Search.TooManyTags"/>
    public virtual async Task<int> GetPostCountAsync(params string[] tagsArg) {
        
        if (!HasPostCountAPI)
            throw new Search.FeatureUnavailable();

        var tags = tagsArg != null
            ? tagsArg.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray()
            : [];

        if (NoMoreThanTwoTags && tags.Length > LimitedTagsSearchCount)
            throw new Search.TooManyTags();

        if (_format is UrlFormat.Philomena or UrlFormat.BooruOnRails) {
            
            var url = CreateUrl(_imageUrl, GetLimit(1), TagsToString(tags));
            var json = await GetJsonAsync(url);
            var token = (JToken)JsonConvert.DeserializeObject(json);
            return token!["total"]!.Value<int>();
            
        } else {
            
            var url = CreateUrl(_imageUrlXml, GetLimit(1), TagsToString(tags));
            var xml = await GetXmlAsync(url);
            return int.Parse(xml.ChildNodes.Item(1)!.Attributes![0].InnerXml);
        }
    }

    /// <summary>
    /// Searches for a random post. If <paramref name="tagsArg"/> array is specified
    /// and isn't empty, random post containing those tags will be returned.
    /// </summary>
    /// <param name="tagsArg">The optional array of tags that must be contained in the post.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    /// <exception cref="Search.TooManyTags"/>
    public virtual async Task<Search.Post.SearchResult> GetRandomPostAsync(params string[] tagsArg) {
        
        var tags = tagsArg != null
            ? tagsArg.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray()
            : [];

        if (NoMoreThanTwoTags && tags.Length > LimitedTagsSearchCount)
            throw new Search.TooManyTags();

        var tagString = TagsToString(tags);

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (_format) {
            
            case UrlFormat.IndexPhp when this is Template.Gelbooru:
                return await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), tagString, "sort=random", $"api_key={Auth.PasswordHash}", $"user_id={Auth.UserId}"));
            
            case UrlFormat.IndexPhp when tags.Length == 0: {
                // We need to request /index.php?page=post&s=random and get the id given by the redirect
                var id = await GetRandomIdAsync(tagString);
                return await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), "id=" + id));
            }
            
            // The previous option doesn't work if there are tags so we contact the XML endpoint to get post count
            case UrlFormat.IndexPhp: {
                
                var url = CreateUrl(_imageUrlXml, GetLimit(1), tagString);
                var xml = await GetXmlAsync(url);
                var max = int.Parse((xml.ChildNodes.Item(1)!.Attributes!)[0].InnerXml);

                if (max == 0) throw new Search.InvalidTags();
                if (SearchIncreasedPostLimit && max > IncreasedPostLimitCount) max = IncreasedPostLimitCount;

                return await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), tagString, "pid=" + Random.Next(0, max)));
            }
            
            case UrlFormat.Philomena:
            case UrlFormat.BooruOnRails:
                return await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), tagString, "random=true", $"login={Auth.UserId}", $"api_key={Auth.PasswordHash}"));
            
            default: return NoMoreThanTwoTags
                ? await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), tagString, "random=true", $"login={Auth.UserId}", $"api_key={Auth.PasswordHash}"))
                : await GetSearchResultFromUrlAsync(CreateUrl(_imageUrl, GetLimit(1), tagString, "order=random", $"login={Auth.UserId}", $"api_key={Auth.PasswordHash}"));
        }
    }

    /// <summary>
    /// Searches for multiple random posts. If <paramref name="tagsArg"/> array is
    /// specified and isn't empty, random posts containing those tags will be returned.
    /// </summary>
    /// <param name="limit">The number of posts to get.</param>
    /// <param name="tagsArg">The optional array of tags that must be contained in the posts.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="Search.FeatureUnavailable"/>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    /// <exception cref="Search.TooManyTags"/>
    public async Task<Search.Post.SearchResult[]> GetRandomPostsAsync(int limit, params string[] tagsArg) {
        
        if (!HasMultipleRandomAPI)
            throw new Search.FeatureUnavailable();

        var tags = tagsArg != null
            ? tagsArg.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray()
            : [];

        if (NoMoreThanTwoTags && tags.Length > LimitedTagsSearchCount)
            throw new Search.TooManyTags();

        var tagString = TagsToString(tags);

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (_format) {
            
            case UrlFormat.IndexPhp:
                return await GetSearchResultsFromUrlAsync(CreateUrl(_imageUrl, GetLimit(limit), tagString) + "+sort:random");
            
            case UrlFormat.Philomena:
            case UrlFormat.BooruOnRails:
                return await GetSearchResultsFromUrlAsync(CreateUrl(_imageUrl, GetLimit(limit), tagString, "sf=random"));
            
            default: {

                if (NoMoreThanTwoTags)
                    // +order:random count as a tag so we use random=true instead to save one
                    return await GetSearchResultsFromUrlAsync(CreateUrl(_imageUrl, GetLimit(limit), tagString, "random=true"));
                
                return await GetSearchResultsFromUrlAsync(CreateUrl(_imageUrl, GetLimit(limit), tagString) + "+order:random");
            }
        }
    }

    /// <summary>
    /// Gets the latest posts on the website. If <paramref name="tagsArg"/> array is
    /// specified and isn't empty, latest posts containing those tags will be returned.
    /// </summary>
    /// <param name="tagsArg">The optional array of tags that must be contained in the posts.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    public virtual async Task<Search.Post.SearchResult[]> GetLastPostsAsync(params string[] tagsArg)
    {
        return GetPostsSearchResult(JsonConvert.DeserializeObject(await GetJsonAsync(CreateUrl(_imageUrl, TagsToString(tagsArg)))));
    }



    /// <summary>
    /// Gets the latest posts on the website. If <paramref name="tagsArg"/> array is
    /// specified and isn't empty, latest posts containing those tags will be returned.
    /// </summary>
    /// <param name="limit">The number of posts to get.</param>
    /// <param name="tagsArg">The optional array of tags that must be contained in the posts.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="System.Net.Http.HttpRequestException"/>
    public virtual async Task<Search.Post.SearchResult[]> GetLastPostsAsync(int limit, params string[] tagsArg)
    {
        return GetPostsSearchResult(JsonConvert.DeserializeObject(await GetJsonAsync(CreateUrl(_imageUrl, "limit=" + limit, TagsToString(tagsArg)))));
    }

    private async Task<Search.Post.SearchResult> GetSearchResultFromUrlAsync(string url) {
        
        return GetPostSearchResult(ParseFirstPostSearchResult(JsonConvert.DeserializeObject(await GetJsonAsync(url))));
    }

    private Task<Search.Post.SearchResult> GetSearchResultFromUrlAsync(Uri url) {
        
        return GetSearchResultFromUrlAsync(url.AbsoluteUri);
    }

    private async Task<Search.Post.SearchResult[]> GetSearchResultsFromUrlAsync(string url)
    {
        return GetPostsSearchResult(JsonConvert.DeserializeObject(await GetJsonAsync(url)));
    }

    private Task<Search.Post.SearchResult[]> GetSearchResultsFromUrlAsync(Uri url)
    {
        return GetSearchResultsFromUrlAsync(url.AbsoluteUri);
    }

    /// <summary>
    /// Converts a letter to its matching <see cref="Search.Post.Rating"/>.
    /// </summary>
    protected Search.Post.Rating GetRating(char c)
    {
        c = char.ToLower(c);
        switch (c)
        {
            case 'g': return Search.Post.Rating.General;
            case 's': return Search.Post.Rating.Safe;
            case 'q': return Search.Post.Rating.Questionable;
            case 'e': return Search.Post.Rating.Explicit;
            default: throw new ArgumentException($"Invalid rating '{c}'.", nameof(c));
        }
    }
}
