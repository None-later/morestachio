﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Morestachio.Framework;
using Morestachio.Helper;

namespace Morestachio
{
	/// <summary>
	///     Provided when parsing a template and getting information about the embedded variables.
	/// </summary>
	public class MorestachioDocumentInfo
	{
		/// <summary>
		///     ctor
		/// </summary>
		/// <param name="parserOptions"></param>
		/// <param name="tokens"></param>
		internal MorestachioDocumentInfo(ParserOptions parserOptions, Queue<TokenPair> tokens)
		{
			ParserOptions = parserOptions;
			TemplateTokens = tokens;
		}

		/// <summary>
		///		The generated tokes from the tokeniser
		/// </summary>
		internal Queue<TokenPair> TemplateTokens { get; }

		public IDocumentItem Document { get; internal set; }

		/// <summary>
		///     The parser Options object that was used to create the Template Delegate
		/// </summary>
		[NotNull]
		public ParserOptions ParserOptions { get; }

		private const int BufferSize = 2024;

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="data"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		public async Task<Stream> CreateAsync([NotNull]object data, CancellationToken token)
		{
			var timeoutCancellation = new CancellationTokenSource();
			if (ParserOptions.Timeout != TimeSpan.Zero)
			{
				timeoutCancellation.CancelAfter(ParserOptions.Timeout);
				var anyCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCancellation.Token);
				token = anyCancellationToken.Token;
			}
			var sourceStream = ParserOptions.SourceFactory();
			try
			{
				if (!sourceStream.CanWrite)
				{
					throw new InvalidOperationException("The stream is ReadOnly");
				}

				using (var byteCounterStream = new ByteCounterStream(sourceStream,
					ParserOptions.Encoding, BufferSize, true))
				{
					var context = new ContextObject(ParserOptions, "")
					{
						Value = data,
						CancellationToken = token
					};
					await Document.Render(byteCounterStream, context, new ScopeData());
				}

				if (timeoutCancellation.IsCancellationRequested)
				{
					sourceStream.Dispose();
					throw new TimeoutException($"The requested timeout of {ParserOptions.Timeout:g} for report generation was reached");
				}
			}
			catch
			{
				//If there is any exception while generating the template we must dispose any data written to the stream as it will never returned and might 
				//create a memory leak with this. This is also true for a timeout
				sourceStream.Dispose();
				throw;
			}
			return sourceStream;
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		public async Task<Stream> CreateAsync([NotNull]object data)
		{
			return await CreateAsync(data, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		[NotNull]
		public async Task<string> CreateAndStringifyAsync([NotNull]object source)
		{
			return await CreateAndStringifyAsync(source, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[NotNull]
		public async Task<string> CreateAndStringifyAsync([NotNull]object source, CancellationToken token)
		{
			return (await CreateAsync(source, token)).Stringify(true, ParserOptions.Encoding);
		}
		
		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		public Stream Create([NotNull]object source, CancellationToken token)
		{
			Stream result = null;
			using (var async = AsyncHelper.Wait)
			{
				async.Run(CreateAsync(source, token), e => result = e);
			}

			return result;
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		[MustUseReturnValue("The Stream contains the template. Use CreateAndStringify() to get the string of it")]
		[NotNull]
		public Stream Create([NotNull]object source)
		{
			return Create(source, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		[NotNull]
		public string CreateAndStringify([NotNull]object source)
		{
			return CreateAndStringify(source, CancellationToken.None);
		}

		/// <summary>
		///     Calls the Underlying Template Delegate and Produces a Stream
		/// </summary>
		/// <param name="source"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		[NotNull]
		public string CreateAndStringify([NotNull]object source, CancellationToken token)
		{
			return Create(source, token).Stringify(true, ParserOptions.Encoding);
		}
	}
}