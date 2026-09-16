using Cassandra;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduPlatform.Data.Cassandra
{
    public class CassandraContext : IDisposable
    {
        private readonly ICluster _cluster;
        private readonly ISession _session;

        public CassandraContext(IConfiguration config)
        {
            var builder = Cluster.Builder()
                .AddContactPoints(config["Cassandra:Host"])
                .WithPort(int.Parse(config["Cassandra:Port"] ?? "9042"));

            var user = config["Cassandra:Username"];
            if (!string.IsNullOrEmpty(user))
                builder.WithCredentials(user, config["Cassandra:Password"]);

            _cluster = builder.Build();
            _session = _cluster.Connect(config["Cassandra:Keyspace"]);
        }

        public ISession Session => _session;

        public void Dispose()
        {
            _session?.Dispose();
            _cluster?.Dispose();
        }
    }
}