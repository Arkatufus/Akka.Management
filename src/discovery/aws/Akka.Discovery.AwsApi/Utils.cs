﻿//-----------------------------------------------------------------------
// <copyright file="Utils.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace Akka.Discovery.AwsApi
{
    public static class IpAddressExtensions
    {
        public static bool IsLoopbackAddress(this IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var test = (byte) 0;
                for(var i=0; i<15; i++)
                {
                    test |= bytes[i];
                }

                return test == 0x00 && bytes[15] == 0x01;
            }
            
            /* 127.x.x.x */
            return bytes[0] == 127;
        }
        
        public static bool IsSiteLocalAddress(this IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6SiteLocal;
            var raw = address.GetAddressBytes();
            
            // refer to RFC 1918
            // 10/8 prefix
            // 172.16/12 prefix
            // 192.168/16 prefix
            return (raw[0] & 0xFF) == 10
                   || ((raw[0] & 0xFF) == 172 && (raw[1] & 0xF0) == 16)
                   || ((raw[0] & 0xFF) == 192 && (raw[1] & 0xFF) == 168);
        }
    }
    
}