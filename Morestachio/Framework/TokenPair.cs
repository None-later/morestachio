using System;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Morestachio.Formatter;

namespace Morestachio.Framework
{
	/// <summary>
	///     The token that has been lexed out of template content.
	/// </summary>
	[DebuggerTypeProxy(typeof(TokenPairDebuggerProxy))]
	public class TokenPair
	{
		[PublicAPI]
		private class TokenPairDebuggerProxy
		{
			private readonly TokenPair _pair;

			public TokenPairDebuggerProxy(TokenPair pair)
			{
				_pair = pair;
			}

			public string Type
			{
				get { return _pair.Type.ToString(); }
			}

			internal Tokenizer.HeaderTokenMatch[] FormatString
			{
				get { return _pair.FormatString; }
			}

			public string Value
			{
				get { return _pair.Value; }
			}

			public override string ToString()
			{
				if (FormatString != null && FormatString.Any())
				{
					return $"{Type} \"{Value}\" AS ({FormatString.Select(e => e.ToString()).Aggregate((e, f) => e + "," + f)})";
				}
				return $"{Type} {Value}";
			}
		}

		/// <summary>
		///		Creates a new Template Token
		/// </summary>
		/// <param name="type"></param>
		/// <param name="value"></param>
		/// <param name="tokenLocation"></param>
		internal TokenPair(TokenType type, string value, Tokenizer.CharacterLocation tokenLocation)
		{
			Type = type;
			Value = value;
			TokenLocation = tokenLocation;
		}

		/// <summary>
		///		Creates a new Template Token
		/// </summary>
		/// <param name="type"></param>
		/// <param name="value"></param>
		/// <param name="tokenLocation"></param>
		internal TokenPair(object type, string value, Tokenizer.CharacterLocation tokenLocation)
		{
			Type = type;
			Value = value;
			TokenLocation = tokenLocation;
		}

		/// <summary>
		///		The type of the Token
		/// </summary>
		public object Type { get; set; }

		[CanBeNull]
		internal Tokenizer.HeaderTokenMatch[] FormatString { get; set; }
		
		/// <summary>
		///		The Token value
		/// </summary>
		[CanBeNull]
		public string Value { get; set; }

		public Tokenizer.CharacterLocation TokenLocation { get; set; }
	}

	public class CustomTokenPair : TokenPair
	{
		public CustomTokenPair(object type, string value, Tokenizer.CharacterLocation tokenLocation) 
			: base(type, value, tokenLocation)
		{
		}

		public string ScopeName { get; private set; }

		public void AddScopeStack(string scopeName)
		{

		}
	}
}