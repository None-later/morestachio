﻿using System;

namespace Morestachio.Framework
{
	/// <summary>
	///     The General Exception type for Framework Exceptions
	/// </summary>
	public class MustachioException : Exception
	{
		/// <summary>
		///     Ctor
		/// </summary>
		/// <param name="message"></param>
		public MustachioException(string message) 
			: base(string.Format(message))
		{
		}
	}
}