﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Morestachio.Document.Contracts;
using Morestachio.Framework;

namespace Morestachio.Document
{
	/// <summary>
	///		Executes the children with a cloned Context
	/// </summary>
	[System.Serializable]
	public class IsolatedContextDocumentItem : DocumentItemBase, IValueDocumentItem
	{
		/// <summary>
		///		Used for XML Serialization
		/// </summary>
		internal IsolatedContextDocumentItem()
		{

		}

		[UsedImplicitly]
		protected IsolatedContextDocumentItem(SerializationInfo info, StreamingContext c) : base(info, c)
		{
		}

		/// <inheritdoc />
		public override string Kind { get; } = "IsolatedContext";

		/// <inheritdoc />
		public override async Task<IEnumerable<DocumentItemExecution>> Render(IByteCounterStream outputStream, ContextObject context, ScopeData scopeData)
		{
			await Task.CompletedTask;
			context = context.CloneForEdit();
			return Children.WithScope(context);
		}

		public async Task<ContextObject> GetValue(ContextObject context, ScopeData scopeData)
		{
			foreach (var valueDocumentItem in Children.OfType<IValueDocumentItem>())
			{
				context = await valueDocumentItem.GetValue(context, scopeData);
			}

			return context;
		}
	}
}