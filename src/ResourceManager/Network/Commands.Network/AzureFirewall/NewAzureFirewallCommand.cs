﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.Azure.Commands.Network.Models;
using Microsoft.Azure.Commands.ResourceManager.Common.Tags;
using Microsoft.Azure.Management.Network;
using MNM = Microsoft.Azure.Management.Network.Models;

namespace Microsoft.Azure.Commands.Network
{
    [Cmdlet(VerbsCommon.New, "AzureRmFirewall", SupportsShouldProcess = true), OutputType(typeof(PSAzureFirewall))]
    public class NewAzureFirewallCommand : AzureFirewallBaseCmdlet
    {
        private PSVirtualNetwork virtualNetwork;
        private PSPublicIpAddress publicIpAddress;

        [Alias("ResourceName")]
        [Parameter(
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The resource name.")]
        [ValidateNotNullOrEmpty]
        public virtual string Name { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The resource group name.")]
        [ValidateNotNullOrEmpty]
        public virtual string ResourceGroupName { get; set; }

        [Parameter(
         Mandatory = true,
         ValueFromPipelineByPropertyName = true,
         HelpMessage = "location.")]
        [ValidateNotNullOrEmpty]
        public virtual string Location { get; set; }

        [Parameter(
         ValueFromPipelineByPropertyName = true,
         HelpMessage = "Virtual Network Name")]
        [ValidateNotNullOrEmpty]
        public string VirtualNetworkName { get; set; }

        [Parameter(
         Mandatory = true,
         ValueFromPipelineByPropertyName = true,
         HelpMessage = "Public Ip Name")]
        [ValidateNotNullOrEmpty]
        public string PublicIpName { get; set; }

        [Parameter(
             Mandatory = false,
             ValueFromPipelineByPropertyName = true,
             HelpMessage = "The list of AzureFirewallApplicationRuleCollections")]
        public List<PSAzureFirewallApplicationRuleCollection> ApplicationRuleCollection { get; set; }

        [Parameter(
             Mandatory = false,
             ValueFromPipelineByPropertyName = true,
             HelpMessage = "The list of AzureFirewallNetworkRuleCollections")]
        public List<PSAzureFirewallNetworkRuleCollection> NetworkRuleCollection { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "A hashtable which represents resource tags.")]
        public Hashtable Tag { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Do not ask for confirmation if you want to overrite a resource")]
        public SwitchParameter Force { get; set; }

        public override void Execute()
        {
            var isVnetPresent = !string.IsNullOrEmpty(VirtualNetworkName);
            var isPublicIpPresent = !string.IsNullOrEmpty(PublicIpName);

            if ((isVnetPresent && !isPublicIpPresent) || (!isVnetPresent && isPublicIpPresent))
            {
                var msg = (isVnetPresent && !isPublicIpPresent) ? $"Virtual Network provided, but Public IP Address was not provided." : $"Public IP Address provided, but Virtual Network name was not provided.";
                throw new ArgumentException(msg);
            }

            // Get the virtual network, get the public IP address
            if (isVnetPresent)
            {
                var vnet = this.VirtualNetworkClient.Get(this.ResourceGroupName, VirtualNetworkName);
                this.virtualNetwork = NetworkResourceManagerProfile.Mapper.Map<PSVirtualNetwork>(vnet);
                
                var publicIp = this.PublicIPAddressesClient.Get(this.ResourceGroupName, PublicIpName);
                this.publicIpAddress = NetworkResourceManagerProfile.Mapper.Map<PSPublicIpAddress>(publicIp);
            }
            
            base.Execute();
            WriteWarning("The output object type of this cmdlet will be modified in a future release.");
            var present = this.IsAzureFirewallPresent(this.ResourceGroupName, this.Name);
            ConfirmAction(
                Force.IsPresent,
                string.Format(Properties.Resources.OverwritingResource, Name),
                Properties.Resources.CreatingResourceMessage,
                Name,
                () => WriteObject(this.CreateAzureFirewall()),
                () => present);
        }

        private PSAzureFirewall CreateAzureFirewall()
        {
            var firewall = new PSAzureFirewall()
            {
                Name = this.Name,
                ResourceGroupName = this.ResourceGroupName,
                Location = this.Location,
                ApplicationRuleCollections = this.ApplicationRuleCollection,
                NetworkRuleCollections = this.NetworkRuleCollection
            };

            if (this.virtualNetwork != null)
            {
                firewall.SetIpConfiguration(this.virtualNetwork, this.publicIpAddress);
            }

            // Map to the sdk object
            var nsgModel = NetworkResourceManagerProfile.Mapper.Map<MNM.AzureFirewall>(firewall);
            nsgModel.Tags = TagsConversionHelper.CreateTagDictionary(this.Tag, validate: true);
            // nsgModel.Sku = null;

            // Execute the Create AzureFirewall call
            this.AzureFirewallClient.CreateOrUpdate(this.ResourceGroupName, this.Name, nsgModel);
            return this.GetAzureFirewall(this.ResourceGroupName, this.Name);
        }
    }
}
