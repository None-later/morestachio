﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;

namespace Morestachio.Formatter.Framework
{
	/// <summary>
	///		Add Extensions for easy runtime added Functions
	/// </summary>
	[PublicAPI]
	public static class MorestachioFormatterServiceExtensions
	{
		/// <summary>
		///     Adds all formatter that are decorated with the <see cref="MorestachioFormatterAttribute" />
		/// </summary>
		/// <param name="type">The type.</param>
		public static void AddFromType(this IMorestachioFormatterService service, Type type)
		{
			foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
			{
				var hasFormatterAttr = method.GetCustomAttributes<MorestachioFormatterAttribute>();
				foreach (var morestachioFormatterAttribute in hasFormatterAttr)
				{
					if (morestachioFormatterAttribute == null)
					{
						continue;
					}

					service.Add(method, morestachioFormatterAttribute);
				}
			}
		}


		public static MultiFormatterInfoCollection AddSingle(this IMorestachioFormatterService service, Delegate function, [CanBeNull]string name = null)
		{
			var morestachioFormatterModel = service.Add(function.Method, new MorestachioFormatterAttribute(name, "Autogenerated description")
			{
				OutputType = function.Method.ReturnType,
				ReturnHint = "None",
			});
			morestachioFormatterModel.FunctionTarget = function.Target;
			return morestachioFormatterModel.MetaData;
		}

		#region Action Overloads

		public static MultiFormatterInfoCollection AddSingle(this IMorestachioFormatterService service, Action function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		public static MultiFormatterInfoCollection AddSingle<T>(this IMorestachioFormatterService service, Action<T> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		public static MultiFormatterInfoCollection AddSingle<T, T1>(this IMorestachioFormatterService service, Action<T, T1> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		#endregion

		#region Function Overloads

		public static MultiFormatterInfoCollection AddSingle<T>(this IMorestachioFormatterService service, Func<T> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		public static MultiFormatterInfoCollection AddSingle<T, T1>(this IMorestachioFormatterService service, Func<T, T1> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		public static MultiFormatterInfoCollection AddSingle<T, T1, T2>(this IMorestachioFormatterService service, Func<T, T1, T2> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		public static MultiFormatterInfoCollection AddSingle<T, T1, T2, T3>(this IMorestachioFormatterService service, Func<T, T1, T2, T3> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		public static MultiFormatterInfoCollection AddSingle<T, T1, T2, T3, T4>(this IMorestachioFormatterService service, Func<T, T1, T2, T3, T4> function, [CanBeNull]string name = null)
		{
			return service.AddSingle((Delegate)function, name);
		}

		#endregion
	}
}
