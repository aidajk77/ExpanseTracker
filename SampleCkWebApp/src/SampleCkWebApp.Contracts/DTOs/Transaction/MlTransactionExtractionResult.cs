using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleCkWebApp.Contracts.DTOs.Transaction
{
    public class MlTransactionExtractionResult
    {
        public string Type { get; set; } = "unknown";
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime? Date { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }

    }
}