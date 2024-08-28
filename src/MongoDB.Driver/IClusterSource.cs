using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Driver.Core.Clusters;

namespace MongoDB.Driver
{
    internal interface IClusterSource
    {
        public ICluster GetOrCreateCluster(ClusterKey key);
        public void ReturnCluster(ICluster cluster);
    }

    internal sealed class DefaultClusterSource : IClusterSource
    {
        public static IClusterSource Instance { get; } = new DefaultClusterSource();

        public void ReturnCluster(ICluster cluster) => ClusterRegistry.Instance.UnregisterAndDisposeCluster(cluster);
        public ICluster GetOrCreateCluster(ClusterKey key) => ClusterRegistry.Instance.GetOrCreateCluster(key);
    }
}
