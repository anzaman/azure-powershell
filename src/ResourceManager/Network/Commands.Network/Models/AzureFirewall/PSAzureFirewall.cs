﻿//
// Copyright (c) Microsoft.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Commands.Common.Attributes;


namespace Microsoft.Azure.Commands.Network.Models
{
    public class PSAzureFirewall : PSTopLevelResource
    {
        private const string AzureFirewallSubnetName = "SecureGatewaySubnet"; // todo: change to "AzureFirewallSubnet";
        private const int AzureFirewallSubnetMinSize = 25;
        private const string AzureFirewallIpConfigurationName = "AzureFirewallIpConfiguration";
        
        public List<PSAzureFirewallIpConfiguration> IpConfigurations { get; set; }

        public List<PSAzureFirewallApplicationRuleCollection> ApplicationRuleCollections { get; set; }

        public List<PSAzureFirewallNetworkRuleCollection> NetworkRuleCollections { get; set; }

        public string ProvisioningState { get; set; }

        [JsonIgnore]
        public string IpConfigurationsText
        {
            get { return JsonConvert.SerializeObject(IpConfigurations, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }); }
        }

        [JsonIgnore]
        public string ApplicationRuleCollectionsText
        {
            get { return JsonConvert.SerializeObject(ApplicationRuleCollections, Formatting.Indented); }
        }

        [JsonIgnore]
        public string NetworkRuleCollectionsText
        {
            get { return JsonConvert.SerializeObject(NetworkRuleCollections, Formatting.Indented); }
        }

        #region Ip Configuration Operations

        public void SetIpConfiguration(PSVirtualNetwork virtualNetwork, PSPublicIpAddress publicIpAddress)
        {
            if (virtualNetwork == null)
            {
                throw new ArgumentNullException(nameof(virtualNetwork), $"Virtual Network cannot be null!");
            }

            if (publicIpAddress == null)
            {
                throw new ArgumentNullException(nameof(publicIpAddress), $"Public IP Address cannot be null!");
            }

            PSSubnet firewallSubnet = null;
            try
            {
                firewallSubnet = virtualNetwork.Subnets.Single(subnet => AzureFirewallSubnetName.Equals(subnet.Name));
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException($"Virtual Network {virtualNetwork.Name} should contain a Subnet named {AzureFirewallSubnetName}");
            }

            var subnetSize = int.Parse(firewallSubnet.AddressPrefix.Split(new[] { '/' })[1]);
            if (subnetSize > AzureFirewallSubnetMinSize)
            {
                throw new ArgumentException($"The AddressPrefix ({firewallSubnet.AddressPrefix}) of the {AzureFirewallSubnetName} of the referenced Virtual Network must be at least /{AzureFirewallSubnetMinSize}");
            }
            
            this.IpConfigurations = new List<PSAzureFirewallIpConfiguration>
                {
                    new PSAzureFirewallIpConfiguration
                    {
                        Name = AzureFirewallIpConfigurationName,
                        Subnet = new PSResourceId { Id = firewallSubnet.Id },
                        PublicIPAddress = new PSResourceId {Id =  publicIpAddress.Id}
                    }
                };
        }

        public void RemoveIpConfiguration()
        {
            this.IpConfigurations = null;
        }

        #endregion // Ip Configuration Operations

        #region Application Rule Collections Operations

        public void AddApplicationRuleCollection(PSAzureFirewallApplicationRuleCollection ruleCollection)
        {
            this.ApplicationRuleCollections = AddRuleCollection(ruleCollection, this.ApplicationRuleCollections);
        }

        public PSAzureFirewallApplicationRuleCollection GetApplicationRuleCollectionByName(string ruleCollectionName)
        {
            return GetRuleCollectionByName(ruleCollectionName, this.ApplicationRuleCollections);
        }

        public PSAzureFirewallApplicationRuleCollection GetApplicationRuleCollectionByPriority(uint priority)
        {
            return this.ApplicationRuleCollections?.FirstOrDefault(rc => rc.Priority == priority);
        }

        public void RemoveApplicationRuleCollectionByName(string ruleCollectionName)
        {
            var ruleCollection = this.GetApplicationRuleCollectionByName(ruleCollectionName);
            this.ApplicationRuleCollections?.Remove(ruleCollection);
        }

        public void RemoveApplicationRuleCollectionByPriority(uint priority)
        {
            var ruleCollection = this.GetApplicationRuleCollectionByPriority(priority);
            this.ApplicationRuleCollections?.Remove(ruleCollection);
        }

        #endregion // Application Rule Collections Operations

        #region Network Rule Collections Operations

        public void AddNetworkRuleCollection(PSAzureFirewallNetworkRuleCollection ruleCollection)
        {
            this.NetworkRuleCollections = AddRuleCollection(ruleCollection, this.NetworkRuleCollections);
        }

        public PSAzureFirewallNetworkRuleCollection GetNetworkRuleCollectionByName(string ruleCollectionName)
        {
            return this.GetRuleCollectionByName(ruleCollectionName, this.NetworkRuleCollections);
        }

        public PSAzureFirewallNetworkRuleCollection GetNetworkRuleCollectionByPriority(uint priority)
        {
            return this.NetworkRuleCollections?.FirstOrDefault(rc => rc.Priority == priority);
        }

        public void RemoveNetworkRuleCollectionByName(string ruleCollectionName)
        {
            var ruleCollection = this.GetNetworkRuleCollectionByName(ruleCollectionName);
            this.NetworkRuleCollections?.Remove(ruleCollection);
        }

        public void RemoveNetworkRuleCollectionByPriority(uint priority)
        {
            var ruleCollection = this.GetNetworkRuleCollectionByPriority(priority);
            this.NetworkRuleCollections?.Remove(ruleCollection);
        }

        #endregion // Application Rule Collections Operations

        #region Private Methods

        private List<BaseRuleCollection> AddRuleCollection<BaseRuleCollection>(BaseRuleCollection ruleCollection, List<BaseRuleCollection> existingRuleCollections) where BaseRuleCollection : PSAzureFirewallBaseRuleCollection
        {
            // Validate
            if (existingRuleCollections != null)
            {
                if (existingRuleCollections.Any(rc => rc.Name.Equals(ruleCollection.Name)))
                {
                    throw new ArgumentException($"Rule Collection names must be unique. {ruleCollection.Name} name is already used.");
                }

                var samePriorityRuleCollections = existingRuleCollections.Where(rc => rc.Priority == ruleCollection.Priority);
                if (existingRuleCollections.Any())
                {
                    throw new ArgumentException($"Rule Collection priorities must be unique. Priority {ruleCollection.Priority} is already used by Rule Collection {samePriorityRuleCollections.First().Name}.");
                }
            }
            else
            {
                existingRuleCollections = new List<BaseRuleCollection>();
            }

            existingRuleCollections.Add(ruleCollection);
            return existingRuleCollections;
        }

        private BaseRuleCollection GetRuleCollectionByName<BaseRuleCollection> (string ruleCollectionName, List<BaseRuleCollection> ruleCollections) where BaseRuleCollection : PSAzureFirewallBaseRuleCollection
        {
            if (null == ruleCollectionName)
            {
                return null;
            }

            return ruleCollections?.FirstOrDefault(rc => ruleCollectionName.Equals(rc.Name));
        }

        #endregion // Private Methods
    }
}
