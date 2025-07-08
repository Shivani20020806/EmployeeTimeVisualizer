using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EmployeeTimeVisualizer
{
    // Data models
    public class TimeEntry
    {
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime StarTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public string EntryNotes { get; set; } = string.Empty;
        public DateTime? DeletedOn { get; set; }
    }

    public class EmployeeTimeData
    {
        public string Name { get; set; } = string.Empty;
        public double TotalHours { get; set; }
    }

    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_URL = "https://rc-vault-fap-live-1.azurewebsites.net/api/gettimeentries?code=vO17RnE8vuzXzPJo5eaLLjXjmRW07law99QTD90zat9FfOQJKKUcgQ==";

        static async Task Main(string[] args)
        {
            try
            {
                // Fetch data from API
                Console.WriteLine("Fetching data from API...");
                var timeEntries = await GetTimeEntriesFromAPI();

                if (timeEntries == null || !timeEntries.Any())
                {
                    Console.WriteLine("No data retrieved from API.");
                    return;
                }

                // Process data
                var employeeData = ProcessTimeEntries(timeEntries);

                // Generate HTML table
                await GenerateHTMLTable(employeeData);

                // Generate PNG pie chart
                GeneratePieChart(employeeData);

                Console.WriteLine("Files generated successfully!");
                Console.WriteLine("- employee_table.html");
                Console.WriteLine("- employee_pie_chart.png");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task<List<TimeEntry>?> GetTimeEntriesFromAPI()
        {
            try
            {
                var response = await httpClient.GetStringAsync(API_URL);
                var timeEntries = JsonConvert.DeserializeObject<List<TimeEntry>>(response);

                // Filter out deleted entries (where DeletedOn is not null)
                return timeEntries?.Where(entry => entry.DeletedOn == null).ToList() ?? new List<TimeEntry>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data: {ex.Message}");
                return new List<TimeEntry>();
            }
        }

        private static List<EmployeeTimeData> ProcessTimeEntries(List<TimeEntry> timeEntries)
        {
            var employeeGroups = timeEntries
                .GroupBy(entry => entry.EmployeeName)
                .Select(group => new EmployeeTimeData
                {
                    Name = group.Key,
                    TotalHours = group.Sum(entry => (entry.EndTimeUtc - entry.StarTimeUtc).TotalHours)
                })
                .OrderByDescending(emp => emp.TotalHours)
                .ToList();

            return employeeGroups;
        }

        private static async Task GenerateHTMLTable(List<EmployeeTimeData> employeeData)
        {
            var htmlBuilder = new StringBuilder();

            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("    <title>Employee Time Report</title>");
            htmlBuilder.AppendLine("    <style>");
            htmlBuilder.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            htmlBuilder.AppendLine("        table { border-collapse: collapse; width: 100%; max-width: 600px; }");
            htmlBuilder.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            htmlBuilder.AppendLine("        th { background-color: #f2f2f2; font-weight: bold; }");
            htmlBuilder.AppendLine("        .low-hours { background-color: #ffebee; }");
            htmlBuilder.AppendLine("        .hours-cell { text-align: right; }");
            htmlBuilder.AppendLine("        h1 { color: #333; }");
            htmlBuilder.AppendLine("    </style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");
            htmlBuilder.AppendLine("    <h1>Employee Time Report</h1>");
            htmlBuilder.AppendLine("    <p>Employees ordered by total time worked (descending)</p>");
            htmlBuilder.AppendLine("    <table>");
            htmlBuilder.AppendLine("        <thead>");
            htmlBuilder.AppendLine("            <tr>");
            htmlBuilder.AppendLine("                <th>Employee Name</th>");
            htmlBuilder.AppendLine("                <th>Total Hours Worked</th>");
            htmlBuilder.AppendLine("            </tr>");
            htmlBuilder.AppendLine("        </thead>");
            htmlBuilder.AppendLine("        <tbody>");

            foreach (var employee in employeeData)
            {
                var rowClass = employee.TotalHours < 100 ? " class=\"low-hours\"" : "";
                htmlBuilder.AppendLine($"            <tr{rowClass}>");
                htmlBuilder.AppendLine($"                <td>{employee.Name}</td>");
                htmlBuilder.AppendLine($"                <td class=\"hours-cell\">{employee.TotalHours:F2}</td>");
                htmlBuilder.AppendLine("            </tr>");
            }

            htmlBuilder.AppendLine("        </tbody>");
            htmlBuilder.AppendLine("    </table>");
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            await File.WriteAllTextAsync("employee_table.html", htmlBuilder.ToString());
            Console.WriteLine("HTML table generated: employee_table.html");
        }

        private static void GeneratePieChart(List<EmployeeTimeData> employeeData)
        {
            const int width = 800;
            const int height = 600;
            const int centerX = width / 2;
            const int centerY = height / 2;
            int radius = Math.Min(width, height) / 3;

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Calculate total hours
                var totalHours = employeeData.Sum(emp => emp.TotalHours);

                // Draw title
                using (var titleFont = new Font("Arial", 16, FontStyle.Bold))
                {
                    var titleText = "Employee Time Distribution";
                    var titleSize = graphics.MeasureString(titleText, titleFont);
                    graphics.DrawString(titleText, titleFont, Brushes.Black,
                        (width - titleSize.Width) / 2, 20);
                }

                // Generate colors for pie slices
                var colors = GenerateColors(employeeData.Count);

                // Draw pie slices
                float startAngle = 0;
                var legendY = 100;

                for (int i = 0; i < employeeData.Count; i++)
                {
                    var employee = employeeData[i];
                    var percentage = (float)(employee.TotalHours / totalHours * 100);
                    var sweepAngle = (float)(employee.TotalHours / totalHours * 360);

                    // Draw pie slice
                    using (var brush = new SolidBrush(colors[i]))
                    {
                        graphics.FillPie(brush, centerX - radius, centerY - radius,
                            radius * 2, radius * 2, startAngle, sweepAngle);
                    }

                    // Draw slice outline
                    graphics.DrawPie(Pens.Black, centerX - radius, centerY - radius,
                        radius * 2, radius * 2, startAngle, sweepAngle);

                    // Draw percentage label on slice
                    if (percentage > 3) // Only show label if slice is large enough
                    {
                        var labelAngle = (startAngle + sweepAngle / 2) * Math.PI / 180;
                        var labelX = centerX + Math.Cos(labelAngle) * radius * 0.7;
                        var labelY = centerY + Math.Sin(labelAngle) * radius * 0.7;

                        using (var font = new Font("Arial", 10, FontStyle.Bold))
                        {
                            var labelText = $"{percentage:F1}%";
                            var labelSize = graphics.MeasureString(labelText, font);
                            graphics.DrawString(labelText, font, Brushes.White,
                                (float)(labelX - labelSize.Width / 2),
                                (float)(labelY - labelSize.Height / 2));
                        }
                    }

                    // Draw legend
                    var legendX = centerX + radius + 50;
                    using (var brush = new SolidBrush(colors[i]))
                    {
                        graphics.FillRectangle(brush, legendX, legendY, 20, 15);
                    }
                    graphics.DrawRectangle(Pens.Black, legendX, legendY, 20, 15);

                    using (var font = new Font("Arial", 10))
                    {
                        var legendText = $"{employee.Name} ({employee.TotalHours:F1}h, {percentage:F1}%)";
                        graphics.DrawString(legendText, font, Brushes.Black, legendX + 25, legendY);
                    }

                    startAngle += sweepAngle;
                    legendY += 25;
                }

                // Save as PNG
                bitmap.Save("employee_pie_chart.png", ImageFormat.Png);
                Console.WriteLine("Pie chart generated: employee_pie_chart.png");
            }
        }

        private static Color[] GenerateColors(int count)
        {
            var colors = new Color[count];
            var hueStep = 360.0 / count;

            for (int i = 0; i < count; i++)
            {
                var hue = (i * hueStep) % 360;
                colors[i] = HSVToRGB(hue, 0.7, 0.9);
            }

            return colors;
        }

        private static Color HSVToRGB(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }
    }
}