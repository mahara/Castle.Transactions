#region License
// Copyright 2004-2022 Castle Project - https://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace Castle.Facilities.AutoTx
{
	using Core.Configuration;

	using MicroKernel.Facilities;

	using Services.Transaction;

	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Reflection;

	/// <summary>
	/// Pendent
	/// </summary>
	public class TransactionMetaInfoStore : MarshalByRefObject
	{
		private static readonly BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		private static readonly string TransactionModeAttribute = "transactionMode";
		private static readonly string IsolationModeAttribute = "isolationLevel";

		private readonly IDictionary _typeToMetaInfo = new HybridDictionary();

		#region MarshalByRefObject overrides

		/// <summary>
		/// Overrides the MBRO Lifetime initialization
		/// </summary>
		/// <returns>Null</returns>
		public override object InitializeLifetimeService()
		{
			return null;
		}

		#endregion

		/// <summary>
		/// Creates meta-information from a type.
		/// </summary>
		public TransactionMetaInfo CreateMetaFromType(Type implementation)
		{
			var metaInfo = new TransactionMetaInfo();

			PopulateMetaInfoFromType(metaInfo, implementation);

			Register(implementation, metaInfo);

			return metaInfo;
		}

		private static void PopulateMetaInfoFromType(TransactionMetaInfo metaInfo, Type implementation)
		{
			if (implementation == typeof(object) || implementation == typeof(MarshalByRefObject))
			{
				return;
			}

			var methods = implementation.GetMethods(BindingFlags);

			foreach (var method in methods)
			{
				var atts = method.GetCustomAttributes(typeof(TransactionAttribute), true);

				if (atts.Length != 0)
				{
					metaInfo.Add(method, atts[0] as TransactionAttribute);
					// only add the method as transaction injection if we also have specified a transaction attribute.
					atts = method.GetCustomAttributes(typeof(InjectTransactionAttribute), true);

					if (atts.Length != 0)
					{
						metaInfo.AddInjection(method);
					}
				}
			}

			PopulateMetaInfoFromType(metaInfo, implementation.BaseType);
		}

		/// <summary>
		/// Create meta-information from the configuration about what methods should be overridden.
		/// </summary>
		public TransactionMetaInfo CreateMetaFromConfig(Type implementation, IList<MethodInfo> methods, IConfiguration config)
		{
			var metaInfo = GetMetaFor(implementation);

			if (metaInfo == null)
			{
				metaInfo = new TransactionMetaInfo();
			}

			foreach (var method in methods)
			{
				var transactionMode = config.Attributes[TransactionModeAttribute];
				var isolationLevel = config.Attributes[IsolationModeAttribute];

				var mode = ObtainTransactionMode(implementation, method, transactionMode);
				var level = ObtainIsolation(implementation, method, isolationLevel);

				metaInfo.Add(method, new TransactionAttribute(mode, level));
			}

			Register(implementation, metaInfo);

			return metaInfo;
		}

		/// <summary>
		/// Gets the meta-data for the implementation.
		/// </summary>
		/// <param name="implementation"></param>
		/// <returns></returns>
		public TransactionMetaInfo GetMetaFor(Type implementation)
		{
			return (TransactionMetaInfo) _typeToMetaInfo[implementation];
		}

		private static TransactionMode ObtainTransactionMode(Type implementation, MethodInfo method, string mode)
		{
			if (mode == null)
			{
				return TransactionMode.Unspecified;
			}

			try
			{
				return (TransactionMode) Enum.Parse(typeof(TransactionMode), mode, true);
			}
			catch (Exception)
			{
				var values = (string[]) Enum.GetValues(typeof(TransactionMode));

				var message = string.Format("The configuration for the class {0}, " +
											"method {1}, has specified {2} on {3} attribute which is not supported. " +
											"The possible values are {4}",
											implementation.FullName,
											method.Name,
											mode,
											TransactionModeAttribute,
											string.Join(", ", values));

				throw new FacilityException(message);
			}
		}

		private IsolationMode ObtainIsolation(Type implementation, MethodInfo method, string level)
		{
			if (level == null)
			{
				return IsolationMode.Unspecified;
			}

			try
			{
				return (IsolationMode) Enum.Parse(typeof(IsolationMode), level, true);
			}
			catch (Exception)
			{
				var values = (string[]) Enum.GetValues(typeof(TransactionMode));

				var message = string.Format("The configuration for the class {0}, " +
											"method {1}, has specified {2} on {3} attribute which is not supported. " +
											"The possible values are {4}",
											implementation.FullName,
											method.Name,
											level,
											IsolationModeAttribute,
											string.Join(", ", values));

				throw new FacilityException(message);
			}
		}

		private void Register(Type implementation, TransactionMetaInfo metaInfo)
		{
			_typeToMetaInfo[implementation] = metaInfo;
		}
	}
}
