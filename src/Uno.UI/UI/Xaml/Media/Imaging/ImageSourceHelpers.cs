﻿#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Uno.Helpers;

internal static class ImageSourceHelpers
{
	private static HttpClient? _httpClient;

	public static async Task<ImageData> ReadFromStreamAsync(Stream stream, CancellationToken ct)
	{
		if (stream.CanSeek && stream.Position != 0)
		{
			stream.Position = 0;
		}

		var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream, 81920, ct);
		var data = memoryStream.ToArray();
		return ImageData.FromBytes(data);
	}

	public static async Task<Stream> OpenStreamFromUriAsync(Uri uri, CancellationToken ct)
	{
		if (uri.IsFile)
		{
			return File.Open(uri.LocalPath, FileMode.Open);
		}

		_httpClient ??= new HttpClient();
		var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct);
		return await response.Content.ReadAsStreamAsync();
	}

	public static async Task<ImageData> GetImageDataFromUri(Uri uri, CancellationToken ct)
	{
		if (uri != null && uri.IsAbsoluteUri)
		{
			if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
				uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
				uri.IsFile)
			{
				using var imageStream = await OpenStreamFromUriAsync(uri, ct);
				return await ReadFromStreamAsync(imageStream, ct);
			}
			else if (uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase))
			{
				var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
				using var fileStream = await file.OpenStreamForReadAsync();
				return await ReadFromStreamAsync(fileStream, ct);
			}
			else if (uri.Scheme.Equals("ms-appdata", StringComparison.OrdinalIgnoreCase))
			{
				using var fileStream = File.OpenRead(AppDataUriEvaluator.ToPath(uri));
				return await ReadFromStreamAsync(fileStream, ct);
			}
		}

		return default;
	}
}
