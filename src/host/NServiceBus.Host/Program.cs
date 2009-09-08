﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Common.Logging;
using NServiceBus.Host.Internal;
using Topshelf;
using Topshelf.Configuration;
using System.Configuration;

namespace NServiceBus.Host
{
    /// <summary>
    /// Entry point to the process.
    /// </summary>
    public class Program
    {
        private static void Main(string[] args)
        {
            Type endpointConfigurationType = GetEndpointConfigurationType();

            AssertThatEndpointConfigurationTypeHasDefaultConstructor(endpointConfigurationType);

            string endpointConfigurationFile = GetEndpointConfigurationFile(endpointConfigurationType);

            if (!File.Exists(endpointConfigurationFile))
            {
                throw new InvalidOperationException("No configuration file found at: " + endpointConfigurationFile);
            }

			var endpointConfiguration = Activator.CreateInstance(endpointConfigurationType);

			string endpointId = GetEndpointId(endpointConfiguration);

            AppDomain.CurrentDomain.SetupInformation.AppDomainInitializerArguments = args;

            IRunConfiguration cfg = RunnerConfigurator.New(x =>
            {
                x.SetDisplayName(endpointId);
                x.SetServiceName(endpointId);
                x.SetDescription("NServiceBus Message Endpoint Host Service");

                x.ConfigureServiceInIsolation<GenericHost>(endpointConfigurationType.AssemblyQualifiedName, c =>
                {
                    c.ConfigurationFile(endpointConfigurationFile);
                    c.CommandLineArguments(args, () => SetHostServiceLocatorArgs);
                    c.WhenStarted(service => service.Start());
                    c.WhenStopped(service => service.Stop());
                    c.CreateServiceLocator(() =>  new HostServiceLocator());
                });

				if (!(endpointConfiguration is ISpecify.ToStartAutomatically))
				{
					x.DoNotStartAutomatically();
				}

				if (endpointConfiguration is ISpecify.ToRunAsLocalSystem)
				{
					x.RunAsLocalSystem();
				}
				else
				{
					x.RunAsFromInteractive();
				}
            });

            Runner.Host(cfg, args);
        }

        private static void SetHostServiceLocatorArgs(string[] args)
        {
            HostServiceLocator.Args = args;
        }

        private static void AssertThatEndpointConfigurationTypeHasDefaultConstructor(Type type)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes);

            if (constructor == null)
                throw new InvalidOperationException("Endpoint configuration type needs to have a default constructor: " + type.FullName);
        }

        private static string GetEndpointConfigurationFile(Type endpointConfigurationType)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                endpointConfigurationType.Assembly.ManifestModule.Name + ".config");
        }

        /// <summary>
        /// Provides a user-friendly name based on the type.
        /// </summary>
        /// <param name="endpointConfigurationType"></param>
        /// <returns></returns>
		public static string GetEndpointId(object endpointConfiguration)
        {
			string endpointName = GetEndpointName(endpointConfiguration);
            return string.Format("{0}_v{1}", endpointName, endpointConfiguration.GetType().Assembly.GetName().Version);
        }

        private static Type GetEndpointConfigurationType()
        {
            string endpoint = ConfigurationManager.AppSettings["EndpointConfigurationType"];
            if (endpoint != null)
                return Type.GetType(endpoint, true);

            IEnumerable<Type> endpoints = ScanAssembliesForEndpoints();

            ValidateEndpoints(endpoints);

            return endpoints.First();
        }

        [DebuggerNonUserCode] //so that exceptions don't jump at the developer debugging their app
        private static IEnumerable<Type> ScanAssembliesForEndpoints()
        {
            IList<Type> result = new List<Type>();
            foreach (var assemblyFile in new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.dll", SearchOption.AllDirectories))
            {
                Type[] types;

                try
                {
                    var assembly = Assembly.LoadFrom(assemblyFile.FullName);
                    
                    types = assembly.GetTypes();
                }
                catch (Exception e)
                {
                    Trace.WriteLine("NServiceBus Host - assembly load failure - ignoring " + assemblyFile + " because of error: " + e);

                    continue;
                }

                foreach (Type type in types.Where(t => typeof(IConfigureThisEndpoint).IsAssignableFrom(t) && t != typeof(IConfigureThisEndpoint)))
                {
                    result.Add(type);
                }
            }

            return result;
        }

        private static void ValidateEndpoints(IEnumerable<Type> endpointConfigurationTypes)
        {
            if (endpointConfigurationTypes.Count() == 0)
            {
                throw new InvalidOperationException("No endpoints found in scanned assemlies, scanned path: " + AppDomain.CurrentDomain.BaseDirectory);
            }

            if (endpointConfigurationTypes.Count() > 1)
            {
                throw new InvalidOperationException("Host doesn't support hosting of multiple endpoints");
            }
        }

        private static string GetEndpointName(object endpointConfiguration)
        {
            string endpointName = null;

        	var iHaveEndpointName = endpointConfiguration as ISpecify.EndpointName;
            if (iHaveEndpointName != null)
            {
                endpointName = iHaveEndpointName.EndpointName;
            }

            if (!string.IsNullOrEmpty(endpointName))
            {
                return endpointName;
            }

            return endpointConfiguration.GetType().FullName;
        }
    }
}