using System;
using System.Collections.Generic;
using System.Text;

namespace Morestachio.Framework
{
	/// <summary>
	///		Defines a custom expression that can be used to extend the default keywords of morestachio
	/// </summary>
	public interface ICustomExpression
	{
		/// <summary>
		///		Tests the extracted token to matches the Custom expression. This should be as fast as possible.
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		bool Test(string token);

		/// <summary>
		///		Creates a new TokenPair when Test is matching.
		/// </summary>
		/// <param name="token">The string contents of that token</param>
		/// <param name="exceptions"></param>
		/// <returns></returns>
		CustomTokenPair CreateToken(string token, IList<Exception> exceptions);
	}
}
