using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2.Services
{
    public static class PointService
    {
        private static readonly MyEntities context = MyEntities.GetContext();
        public static Point GetPointByName(string pointName)
        {
            return context.Points.FirstOrDefault(p => p.PointName == pointName);
        }

        public static List<Point> GetAllPoints()
        {
            return context.Points.ToList();
        }
    }
}
