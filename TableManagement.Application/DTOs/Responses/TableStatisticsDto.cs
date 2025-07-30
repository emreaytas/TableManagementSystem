using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class TableStatisticsDto
    {
        public int TotalTables { get; set; }
        public int TablesThisMonth { get; set; }
        public double AverageColumnsPerTable { get; set; }
     
    }
}
