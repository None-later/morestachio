﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Morestachio.Attributes;
using Morestachio.Formatter.Framework.Converter;
using Morestachio.Helper;

namespace Morestachio.Formatter.Framework
{
	/// <summary>
	///     The Formatter service that can be used to interpret the Native C# formatter.
	///     To use this kind of formatter you must create a public static class where all formatting functions are located.
	///     Then create a public static function that accepts n arguments of the type you want to format. For Example:
	///     If the formatter should be only used for int formatting and the argument will always be a string you have to create
	///     a function that has this header.
	///     It must not return a value.
	///     The functions must be annotated with the MorestachioFormatter attribute
	/// </summary>
	public class MorestachioFormatterService : IMorestachioFormatterService
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="MorestachioFormatterService" /> class.
		/// </summary>
		public MorestachioFormatterService()
		{
			Formatters = new List<MorestachioFormatterModel>();
			ValueConverter = new List<IFormatterValueConverter>();
			DefaultConverter = new GenericTypeConverter();
		}

		/// <summary>
		///     Gets the gloabl formatter that are used always for any formatting run.
		/// </summary>
		public ICollection<MorestachioFormatterModel> Formatters { get; }

		/// <summary>
		///		List of all Value Converters that can be used to convert formatter arguments
		/// </summary>
		public ICollection<IFormatterValueConverter> ValueConverter { get; }

		/// <summary>
		///		The fallback Converter that should convert all known mscore lib types
		/// </summary>
		public IFormatterValueConverter DefaultConverter { get; set; }

		/// <summary>
		///     If set writes the Formatters log.
		/// </summary>
		[CanBeNull]
		public TextWriter FormatterLog { get; set; }
		
		/// <summary>
		///     Writes the specified log.
		/// </summary>
		/// <param name="log">The log.</param>
		public void Log(Func<string> log)
		{
			FormatterLog?.WriteLine(log());
		}

		/// <summary>
		///     Can be returned by a Formatter to control what formatter should be used
		/// </summary>
		public struct FormatterFlow
		{
			/// <summary>
			///     Return code for all formatters to skip the execution of the current formatter and try another one that could also
			///     match
			/// </summary>
			public static FormatterFlow Skip { get; } = new FormatterFlow();
		}

		/// <inheritdoc />
		public virtual async Task<object> CallMostMatchingFormatter(
			Type type,
			KeyValuePair<string, object>[] values,
			object sourceValue,
			string name)
		{
			Log(() => "---------------------------------------------------------------------------------------------");
			Log(() => $"Call Formatter for Type '{type}' on '{sourceValue}'");
			var hasFormatter = GetMatchingFormatter(type, values, name).Where(e => e != null);

			foreach (var formatTemplateElement in hasFormatter)
			{
				Log(() =>
					$"Try formatter '{formatTemplateElement.InputType}' on '{formatTemplateElement.Function.Name}'");
				var executeFormatter = await Execute(formatTemplateElement, sourceValue, values);
				if (!Equals(executeFormatter, FormatterFlow.Skip))
				{
					Log(() => $"Success. return object {executeFormatter}");
					return executeFormatter;
				}

				Log(() => $"Formatter returned '{executeFormatter}'. Try another");
			}

			Log(() => "No Formatter has matched. Skip and return Source Value.");

			return FormatterFlow.Skip;
		}

		/// <summary>
		///     Executes the specified formatter.
		/// </summary>
		/// <param name="formatter">The formatter.</param>
		/// <param name="sourceObject">The source object.</param>
		/// <param name="templateArguments">The template arguments.</param>
		/// <returns></returns>
		public virtual async Task<object> Execute(MorestachioFormatterModel formatter,
			object sourceObject,
			params KeyValuePair<string, object>[] templateArguments)
		{
			var values = ComposeValues(formatter, sourceObject, formatter.Function, templateArguments);

			if (values.Equals(new FormatterComposingResult()))
			{
				Log(() => "Skip: Execute skip as Compose Values returned an invalid value");
				return FormatterFlow.Skip;
			}

			Log(() => "Execute");
			var taskAlike = values.MethodInfo.Invoke(formatter.FunctionTarget, values.Arguments.Select(e => e.Value).ToArray());

			return await taskAlike.UnpackFormatterTask();
		}

		/// <summary>
		///     Gets the matching formatter.
		/// </summary>
		/// <param name="typeToFormat">The type to format.</param>
		/// <param name="arguments"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public virtual IEnumerable<MorestachioFormatterModel> GetMatchingFormatter([NotNull] Type typeToFormat,
			[NotNull] KeyValuePair<string, object>[] arguments,
			[CanBeNull] string name)
		{
			Log(() =>
			{
				var aggregate = arguments.Any() ? arguments.Select(e => $"[{e.Key}]:\"{e.Value}\"").Aggregate((e, f) => e + " & " + f) : "";
				return
					$"Test Filter for '{typeToFormat}' with arguments '{aggregate}'";
			});

			var filteredSourceList = new List<KeyValuePair<MorestachioFormatterModel, ulong>>();
			foreach (MorestachioFormatterModel formatTemplateElement in Formatters)
			{
				if (!string.Equals(formatTemplateElement.Name, name))
				{
					continue;
				}

				Log(() => $"Test filter: '{formatTemplateElement.InputType} : {formatTemplateElement.Function.Name}'");

				if (formatTemplateElement.InputType != typeToFormat &&
					!formatTemplateElement.InputType.GetTypeInfo().IsAssignableFrom(typeToFormat))
				{
					var typeToFormatGenerics = typeToFormat.GetTypeInfo().GetGenericArguments();

					//explicit check for array support
					if (typeToFormat.HasElementType)
					{
						var elementType = typeToFormat.GetElementType();
						typeToFormatGenerics = typeToFormatGenerics.Concat(new[] { elementType }).ToArray();
					}

					//the type check has maybe failed because of generic parameter. Check if both the formatter and the typ have generic arguments

					var formatterGenerics = formatTemplateElement.InputType.GetTypeInfo().GetGenericArguments();

					if (typeToFormatGenerics.Length <= 0 || formatterGenerics.Length <= 0 ||
						typeToFormatGenerics.Length != formatterGenerics.Length)
					{
						Log(() =>
							$"Exclude because formatter accepts '{formatTemplateElement.InputType}' is not assignable from '{typeToFormat}'");
						continue;
					}
				}

				//count rest arguments
				var mandatoryArguments = formatTemplateElement.MetaData
					.Where(e => !e.IsRestObject && !e.IsOptional && !e.IsSourceObject).ToArray();
				if (mandatoryArguments.Length > arguments.Length)
				//if there are less arguments excluding rest then parameters
				{
					Log(() =>
						"Exclude because formatter has " +
						$"'{mandatoryArguments.Length}' " +
						"parameter and " +
						$"'{formatTemplateElement.MetaData.Count(e => e.IsRestObject)}' " +
						"rest parameter but needs less or equals" +
						$"'{arguments.Length}'.");
					continue;
				}

				ulong score = 1L;
				if (formatTemplateElement.Function.ReturnParameter == null ||
					formatTemplateElement.Function.ReturnParameter?.ParameterType == typeof(void))
				{
					score++;
				}

				score += (ulong)(arguments.Length - mandatoryArguments.Length);
				Log(() => $"Take filter: '{formatTemplateElement.InputType} : {formatTemplateElement.Function}' Score {score}");
				filteredSourceList.Add(new KeyValuePair<MorestachioFormatterModel, ulong>(formatTemplateElement, score));
			}

			foreach (var formatTemplateElement in filteredSourceList.OrderBy(e => e.Value))
			{
				yield return formatTemplateElement.Key;
			}
		}

		public struct FormatterComposingResult
		{
			public MethodInfo MethodInfo { get; set; }
			public IDictionary<MultiFormatterInfo, object> Arguments { get; set; }
		}

		/// <summary>
		///     Composes the values into a Dictionary for each formatter. If returns null, the formatter will not be called.
		/// </summary>
		public virtual FormatterComposingResult ComposeValues(
			[NotNull] MorestachioFormatterModel formatter,
			[CanBeNull] object sourceObject,
			[NotNull] MethodInfo method,
			[NotNull] params KeyValuePair<string, object>[] templateArguments)
		{
			Log(() =>
				$"Compose values for object '{sourceObject}' with formatter '{formatter.InputType}' targets '{formatter.Function.Name}'");
			var values = new Dictionary<MultiFormatterInfo, object>();
			var matched = new Dictionary<MultiFormatterInfo, Tuple<string, object>>();

			var templateArgumentsQueue = templateArguments.Select(e => Tuple.Create(e.Key, e.Value)).ToList();

			var argumentIndex = 0;
			foreach (var parameter in formatter.MetaData.Where(e => !e.IsRestObject))
			{
				argumentIndex++;
				Log(() => $"Match parameter '{parameter.ParameterType}' [{parameter.Name}]");
				object givenValue;
				//set ether the source object or the value from the given arguments
				
				if (parameter.IsSourceObject)
				{
					Log(() => "Is Source object");
					givenValue = sourceObject;
				}
				else
				{
					//match by index or name
					Log(() => "Try Match by Name");
					//match by name
					var match = templateArgumentsQueue.FirstOrDefault(e =>
						!string.IsNullOrWhiteSpace(e.Item1) && e.Item1.Equals(parameter.Name));

					if (match == null)
					{
						Log(() => "Try Match by Index");
						//match by index
						var index = 0;
						match = templateArgumentsQueue.FirstOrDefault(g => index++ == parameter.Index);
					}

					if (match == null)
					{
						if (parameter.IsOptional)
						{
							givenValue = null;
							match = new Tuple<string, object>(parameter.Name, null);
						}
						else
						{
							Log(() => $"Skip: Could not match the parameter at index '{parameter.Index}' nether by name nor by index");
							return default;
						}
					}
					else
					{
						givenValue = match.Item2;
						Log(() => $"Matched '{match.Item1}': '{match.Item2}' by Name/Index");

						//check for matching types
						if (!parameter.ParameterType.GetTypeInfo().IsAssignableFrom(givenValue?.GetType()))
						{
							var typeConverter =
								ValueConverter.FirstOrDefault(e => e.CanConvert(givenValue, parameter.ParameterType));
							typeConverter = typeConverter ??
							                (DefaultConverter.CanConvert(givenValue, parameter.ParameterType) ? DefaultConverter : null);
							var perParameterConverter = parameter.FormatterValueConverterAttribute
								.Select(f => f.CreateInstance())
								.FirstOrDefault(e => e.CanConvert(givenValue, parameter.ParameterType));

							if (perParameterConverter != null)
							{
								givenValue = perParameterConverter.Convert(givenValue, parameter.ParameterType);
							}
							else if (typeConverter != null)
							{
								givenValue = typeConverter.Convert(givenValue, parameter.ParameterType);
							}
							else if (givenValue is IConvertible convertible &&
							         typeof(IConvertible).IsAssignableFrom(parameter.ParameterType))
							{
								givenValue = convertible.ToType(parameter.ParameterType, CultureInfo.CurrentCulture);
							}
							else
							{
								Log(() =>
									$"Skip: Match is Invalid because type at {argumentIndex} of '{parameter.ParameterType.Name}' was not expected. Abort.");
								//The type in the template and the type defined in the formatter do not match. Abort	

								return default;
							}
						}
					}
					matched.Add(parameter, match);
				}

				var localGen = parameter.ParameterType.GetGenericArguments();
				var valueType = givenValue?.GetType();
				var templateGen = valueType?.GetGenericArguments();
				if (parameter.ParameterType.ContainsGenericParameters && templateGen != null)
				{
					if (localGen.Any() != templateGen.Any())
					{
						if (valueType.IsArray)
						{
							templateGen = new[] { valueType.GetElementType() };
						}
						else
						{
							Log(() =>
								$"{nameof(MorestachioFormatterService)}| Generic type mismatch");
							continue;
						}
					}

					if (!parameter.ParameterType.ContainsGenericParameters)
					{
						Log(() =>
							$"{nameof(MorestachioFormatterService)}| Type has Generic but Method not");
						continue;
					}

					if (localGen.Length != templateGen.LongLength)
					{
						Log(() =>
							$"{nameof(MorestachioFormatterService)}| Generic type count mismatch");
						continue;
					}

					method = method.MakeGenericMethod(templateGen);
				}

				values.Add(parameter, givenValue);
				if (parameter.IsOptional || parameter.IsSourceObject)
				{
					continue; //value and source object are optional so we do not to check for its existence 
				}

				if (Equals(givenValue, null))
				{
					Log(() =>
						"Skip: Match is Invalid because template value is null where the Formatter does not have a optional value");
					//the delegates parameter is not optional so this formatter does not fit. Continue.
					return default;
				}
			}

			var hasRest = formatter.MetaData.FirstOrDefault(e => e.IsRestObject);
			if (hasRest == null)
			{
				return new FormatterComposingResult()
				{
					MethodInfo = method,
					Arguments = values
				};
			}

			templateArgumentsQueue.RemoveAll(e => matched.Values.Contains(e));

			Log(() => $"Match Rest argument '{hasRest.ParameterType}'");

			//{{"test", Buffer.X, "1"}}
			//only use the values that are not matched.
			if (typeof(KeyValuePair<string, object>[]) == hasRest.ParameterType)
			{
				//keep the name value pairs
				values.Add(hasRest, templateArgumentsQueue);
			}
			else if (typeof(object[]).GetTypeInfo().IsAssignableFrom(hasRest.ParameterType))
			{
				//its requested to transform the rest values and truncate the names from it.
				values.Add(hasRest, templateArgumentsQueue.Select(e => e.Item2).ToArray());
			}
			else
			{
				Log(() => $"Skip: Match is Invalid because  '{hasRest.ParameterType}' is no supported rest parameter");
				//unknown type in params argument cannot call
				return default;
			}
			return new FormatterComposingResult()
			{
				MethodInfo = method,
				Arguments = values
			};
		}

		/// <inheritdoc />
		public MorestachioFormatterModel Add(MethodInfo method, MorestachioFormatterAttribute morestachioFormatterAttribute)
		{
			var arguments = method.GetParameters().Select((e, index) =>
				new MultiFormatterInfo(
					e.ParameterType,
					e.GetCustomAttribute<FormatterArgumentNameAttribute>()?.Name ?? e.Name,
					e.IsOptional,
					index,
					e.GetCustomAttribute<ParamArrayAttribute>() != null ||
					e.GetCustomAttribute<RestParameterAttribute>() != null)
				{
					IsSourceObject = e.GetCustomAttribute<SourceObjectAttribute>() != null,
					FormatterValueConverterAttribute = e.GetCustomAttributes<FormatterValueConverterAttribute>().ToArray()
				}).ToArray();
			//if there is no declared SourceObject then check if the first object is of type what we are formatting and use this one.
			if (!arguments.Any(e => e.IsSourceObject) && arguments.Any())
			{
				arguments[0].IsSourceObject = true;
			}

			var sourceValue = arguments.FirstOrDefault(e => e.IsSourceObject);
			if (sourceValue != null)
			{
				//if we have a source value in the arguments reduce the index of all following 
				//this is important as the source value is never in the formatter string so we will not "count" it 
				for (var i = sourceValue.Index; i < arguments.Length; i++)
				{
					arguments[i].Index--;
				}

				sourceValue.Index = -1;
			}

			var morestachioFormatterModel = new MorestachioFormatterModel(morestachioFormatterAttribute.Name,
				morestachioFormatterAttribute.Description,
				arguments.FirstOrDefault(e => e.IsSourceObject)?.ParameterType ?? typeof(object),
				morestachioFormatterAttribute.OutputType ?? method.ReturnType,
				method.GetCustomAttributes<MorestachioFormatterInputAttribute>()
					.Select(e => new InputDescription(e.Description, e.OutputType, e.Example)).ToArray(),
				morestachioFormatterAttribute.ReturnHint,
				method,
				new MultiFormatterInfoCollection(arguments));
			Formatters.Add(morestachioFormatterModel);
			return morestachioFormatterModel;
		}
	}
}