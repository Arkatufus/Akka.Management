﻿// -----------------------------------------------------------------------
//  <copyright file="ClusterBootstrapOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Reflection;
using System.Text;
using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;

namespace Akka.Management.Cluster.Bootstrap;

public class ClusterBootstrapOptions
{
    /// <summary>
    /// Cluster Bootstrap will always attempt to join an existing cluster if possible. However
    /// if no contact point advertises any seed-nodes a new cluster will be formed by the
    /// node with the lowest address as decided by the <see cref="LowestAddressJoinDecider"/>.
    /// Setting NewClusterEnabled to false after an initial cluster has formed is recommended to prevent new clusters
    /// forming during a network partition when nodes are redeployed or restarted.
    /// </summary>
    public bool? NewClusterEnabled { get; set; }

    /// <summary>
    /// Configuration for the first phase of bootstrapping, during which contact points are discovered
    /// using the configured service discovery mechanism (e.g. DNS records).
    /// </summary>
    public ContactPointDiscoveryOptions ContactPointDiscovery { get; } = new();

    /// <summary>
    /// Configure how we communicate with the contact point once it is discovered
    /// </summary>
    public ContactPointOptions ContactPoint { get; } = new();

    /// <summary>
    /// Join decider class configuration
    /// </summary>
    public JoinDeciderOptions JoinDecider { get; } = new();

    public void Apply(AkkaConfigurationBuilder builder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("akka.management.cluster.bootstrap {");
        if (NewClusterEnabled is { })
            sb.AppendLine($"new-cluster-enabled = {NewClusterEnabled.ToHocon()}");
        
        ContactPointDiscovery?.Apply(sb);
        ContactPoint?.Apply(sb);
        JoinDecider?.Apply(sb);
        
        sb.AppendLine("}");

        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
    }
}

public sealed class ContactPointDiscoveryOptions
{
    /// <summary>
    /// Define this name to be looked up in service discovery for "neighboring" nodes
    /// If undefined, the name will be taken from the AKKA__CLUSTER__BOOTSTRAP__SERVICE_NAME
    /// environment variable or extracted from the ActorSystem name
    /// </summary>
    public string? ServiceName { get; set; }
    
    /// <summary>
    /// Added as suffix to the service-name to build the effective-service name used in the contact-point service lookups
    /// If undefined, nothing will be appended to the service-name.
    /// 
    /// Examples, set this to:
    /// "default.svc.cluster.local" or "my-namespace.svc.cluster.local" for kubernetes clusters.
    /// </summary>
    public string? ServiceNamespace { get; set; }
    
    /// <summary>
    /// The portName passed to discovery. This should be set to the name of the port for Akka Management
    /// </summary>
    public string? PortName { get; set; }
    
    /// <summary>
    /// The protocol passed to discovery.
    /// </summary>
    public string? Protocol { get; set; }
    
    /// <summary>
    /// Config path of discovery method to be used to locate the initial contact points.
    /// It must be a fully qualified config path to the discovery's config section.
    /// 
    /// By setting this to `akka.discovery` we ride on the configuration mechanisms that akka-discovery has,
    /// and reuse what is configured for it. You can set it explicitly to something else here, if you want to
    /// use a different discovery mechanism for the bootstrap than for the rest of the application.
    /// </summary>
    public string? DiscoveryMethod { get; set; }
    
    /// <summary>
    /// The effective service name is the exact string that will be used to perform service discovery.
    /// 
    /// Set this value to a specific string to override the default behaviour of building the effective name by
    /// concatenating the `service-name` with the optional `service-namespace` (e.g. "name.default").
    /// </summary>
    public string? EffectiveName { get; set; }
    
    /// <summary>
    /// Amount of time for which a discovery observation must remain "stable"
    /// (i.e. discovered contact-points list did not change) before a join decision can be made.
    /// This is done to decrease the likelihood of performing decisions on fluctuating observations.
    /// 
    /// This timeout represents a tradeoff between safety and quickness of forming a new cluster.
    /// </summary>
    public TimeSpan? StableMargin { get; set; }
    
    /// <summary>
    /// Interval at which service discovery will be polled in search for new contact-points
    /// 
    /// Note that actual timing of lookups will be the following:
    /// - perform initial lookup; interval is this base interval
    /// - await response within resolve-timeout
    ///   (this can be larger than interval, which means interval effectively is resolveTimeout + interval,
    ///   this has been specifically made so, to not hit discovery services with requests while the lookup is being serviced)
    /// - if failure happens apply backoff to interval (the backoff growth is exponential)
    /// - if no failure happened, and we receive a resolved list of services, schedule another lookup in interval time
    /// - if previously failures happened during discovery, a successful lookup resets the interval to `interval` again
    /// - repeat until stable-margin is reached
    /// </summary>
    public TimeSpan? Interval { get; set; }
    
    /// <summary>
    /// Adds "noise" to vary the intervals between retries slightly (0.2 means 20% of base value).
    /// This is important in order to avoid the various nodes performing lookups in the same interval,
    /// potentially causing a thundering heard effect. Usually there is no need to tweak this parameter.
    /// </summary>
    public double? ExponentialBackoffRandomFactor { get; set; }
    
    /// <summary>
    /// Maximum interval to which the exponential backoff is allowed to grow
    /// </summary>
    public TimeSpan? ExponentialBackoffMax { get; set; }
    
    /// <summary>
    /// The smallest number of contact points that need to be discovered before the bootstrap process can start.
    /// For optimal safety during cluster formation, you may want to set these value to the number of initial
    /// nodes that you know will participate in the cluster (e.g. the value of `spec.replicas` as set in your kubernetes config.
    /// </summary>
    public int? RequiredContactPointsNr { get; set; }
    
    /// <summary>
    /// Does a successful response have to be received by all contact points.
    /// Used by the LowestAddressJoinDecider.
    /// Can be set to false in environments where old contact points may still be in service discovery
    /// or when using local discovery and cluster formation is desired without starting all the nodes
    /// Required-contact-point-nr still needs to be met
    /// </summary>
    public bool? ContactWithAllContactPoints { get; set; }
    
    /// <summary>
    /// Timeout for getting a reply from the service-discovery subsystem
    /// </summary>
    public TimeSpan? ResolveTimeout { get; set; }


    internal void Apply(StringBuilder sb)
    {
        sb.AppendLine("contact-point-discovery {");

        if (ServiceName is { })
            sb.AppendLine($"service-name = {ServiceName.ToHocon()}");
        if (ServiceNamespace is { })
            sb.AppendLine($"service-namespace = {ServiceNamespace.ToHocon()}");
        if (PortName is { })
            sb.AppendLine($"port-name = {PortName.ToHocon()}");
        if (Protocol is { })
            sb.AppendLine($"protocol = {Protocol.ToHocon()}");
        if (DiscoveryMethod is { })
            sb.AppendLine($"discovery-method = {DiscoveryMethod.ToHocon()}");
        if (EffectiveName is { })
            sb.AppendLine($"effective-name = {EffectiveName.ToHocon()}");
        if (StableMargin is { })
            sb.AppendLine($"stable-margin = {StableMargin.ToHocon()}");
        if (Interval is { })
            sb.AppendLine($"interval = {Interval.ToHocon()}");
        if (ExponentialBackoffRandomFactor is { })
            sb.AppendLine($"exponential-backoff-random-factor = {ExponentialBackoffRandomFactor}");
        if (ExponentialBackoffMax is { })
            sb.AppendLine($"exponential-backoff-max = {ExponentialBackoffMax.ToHocon()}");
        if (RequiredContactPointsNr is { })
            sb.AppendLine($"required-contact-point-nr = {RequiredContactPointsNr}");
        if (ContactWithAllContactPoints is { })
            sb.AppendLine($"contact-with-all-contact-points = {ContactWithAllContactPoints.ToHocon()}");
        if (ResolveTimeout is { })
            sb.AppendLine($"resolve-timeout = {ResolveTimeout.ToHocon()}");
        
        sb.AppendLine("}");
    }
}

public sealed class ContactPointOptions
{
    /// <summary>
    /// If no port is discovered along with the host/ip of a contact point this port will be used as fallback
    /// Also, when no port-name is used and multiple results are returned for a given service with at least one
    /// port defined, this port is used to disambiguate. 
    /// </summary>
    public int? FallbackPort { get; set; }
    
    /// <summary>
    /// By default, when no port-name is set, only the contact points that contain the fallback-port
    /// are used for probing. This makes the scenario where each akka node has multiple ports
    /// returned from service discovery (e.g. management, remoting, front-end HTTP) work without
    /// having to configure a port-name. If instead service discovery will return only akka management
    /// ports without specifying a port-name, e.g. management has dynamic ports and its own service
    /// name, then set this to false to stop the results being filtered
    /// </summary>
    public bool? FilterOnFallbackPort { get; set; }
    
    /// <summary>
    /// If some discovered seed node will keep failing to connect for specified period of time,
    /// it will initiate rediscovery again instead of keep trying.
    /// </summary>
    public TimeSpan? ProbingFailureTimeout { get; set; }
    
    /// <summary>
    /// Interval at which contact points should be polled
    /// the effective interval used is this value plus the same value multiplied by the jitter value
    /// </summary>
    public TimeSpan? ProbeInterval { get; set; }
    
    /// <summary>
    /// Max amount of jitter to be added on retries
    /// </summary>
    public double? ProbeIntervalJitter { get; set; }
    
    internal void Apply(StringBuilder sb)
    {
        sb.AppendLine("contact-point {");
        
        if (FallbackPort is { })
            sb.AppendLine($"fallback-port = {FallbackPort.ToHocon()}");
        if (FilterOnFallbackPort is { })
            sb.AppendLine($"filter-on-fallback-port = {FilterOnFallbackPort.ToHocon()}");
        if (ProbingFailureTimeout is { })
            sb.AppendLine($"probing-failure-timeout = {ProbingFailureTimeout.ToHocon()}");
        if (ProbeInterval is { })
            sb.AppendLine($"probe-interval = {ProbeInterval.ToHocon()}");
        if (ProbeIntervalJitter is { })
            sb.AppendLine($"probe-interval-jitter = {ProbeIntervalJitter.ToHocon()}");
        
        sb.AppendLine("}");
    }
}

public sealed class JoinDeciderOptions 
{
    /// <summary>
    /// Implementation of JoinDecider.
    /// It must extend akka.management.cluster.bootstrap.JoinDecider and
    /// have public constructor with ActorSystem and ClusterBootstrapSettings parameters.
    /// </summary>
    public Type? Class { get; set; }

    internal void Apply(StringBuilder sb)
    {
        if (Class is null)
            return;
        
        ValidateType(Class, nameof(Class));
        sb.AppendLine("join-decider {");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");
        sb.AppendLine("}");
    }
    
    private static void ValidateType(Type type, string paramName)
    {
        if (!typeof(IJoinDecider).IsAssignableFrom(type))
            throw new ConfigurationException($"{paramName} Type value need to extend {nameof(IJoinDecider)}. Was: {type.Name}");

        var ctorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new [] {typeof(ActorSystem), typeof(ClusterBootstrapSettings)}, null);
        if (ctorInfo is null)
            throw new ConfigurationException(
                $"{paramName} Type value need to have a constructor that takes 2 parameters, {nameof(ActorSystem)} and {nameof(ClusterBootstrapSettings)}");
    }    
}