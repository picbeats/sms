﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;

namespace Sms.Router
{
	public class FileBasedConfiguration : IRouterConfiguration
	{
		private Dictionary<string, ServiceEndpoint> services;
		private Dictionary<string, List<string>> serviceMapping = new Dictionary<string, List<string>>();

		public IEnumerable<ServiceEndpoint> Get(string serviceName)
		{
			if(services == null)
				throw new InvalidOperationException("Service configuration not set, service name: " + serviceName);

			if(!services.ContainsKey(serviceName))
				yield break;

			yield return services[serviceName];
		}

		public void Add(ServiceEndpoint service)
		{
			lock (this)
			{
					services[service.MessageType] = service;
			}
		}

		public void AddMapping(string fromMessageType, string toMessageType)
		{
			lock (this)
			{
				if (!serviceMapping.ContainsKey(fromMessageType))
					serviceMapping[fromMessageType] = new List<string>();

				serviceMapping[fromMessageType].Add(toMessageType);
			}
		}

		public void Remove(ServiceEndpoint service)
		{
			lock (this)
			{
				if (!services.ContainsKey(service.MessageType))
				{
					return;
				}

				services.Remove(service.MessageType);
			}
		}

		public void RemoveMapping(string fromMessageType, string toMessageType)
		{
			lock (this)
			{
				if (!serviceMapping.ContainsKey(fromMessageType))
					return;

				serviceMapping[fromMessageType].RemoveAll(x => x == toMessageType);
			}
		}

		public void Load(IEnumerable<ServiceEndpoint> endpoints)
		{
			lock (this)
			{
				services = endpoints.ToDictionary(x => x.MessageType, x => x, StringComparer.InvariantCultureIgnoreCase);
			}
		}

		public static FileBasedConfiguration LoadConfiguration()
		{
			var section = (NameValueCollection)ConfigurationManager.GetSection("ServiceConfiguration");

			if (section == null)
			{
				Logger.Warn("Configuration could not be read");
				return new FileBasedConfiguration();
			}

			if (section.Keys.Count == 0)
			{
				Logger.Warn("Configuration does not contain any services, configuration key count == 0");
				return new FileBasedConfiguration();
			}

			var services = new List<ServiceEndpoint>();

			foreach (string key in section.Keys)
			{
				string value = section[key];

				var valueSplit = value.Split(new[] { "://" }, 2, StringSplitOptions.RemoveEmptyEntries);

				string provider = valueSplit[0];
				string queueName = valueSplit[1];

				services.Add(new ServiceEndpoint()
				{
					MessageType = key,
					ProviderName = provider,
					QueueIdentifier = queueName
				});
			}
			var result = new FileBasedConfiguration();
			result.Load(services);
			return result;
		}
	}
}