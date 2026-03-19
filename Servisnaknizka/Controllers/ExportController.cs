using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servisnaknizka.Data;
using Servisnaknizka.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Text;

namespace Servisnaknizka.Controllers;

[Route("api/export")]
[ApiController]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public ExportController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("pdf/{vehicleId}")]
    public async Task<IActionResult> ExportVehiclePdf(int vehicleId)
    {
        // Overenie prihlásenia
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Redirect("/login");

        // Nájdenie vozidla
        var vehicle = await _context.Vehicles
            .Where(v => v.Id == vehicleId && v.IsActive)
            .Include(v => v.Owner)
            .Include(v => v.ServiceRecords)
            .ThenInclude(sr => sr.CreatedBy)
            .FirstOrDefaultAsync();

        if (vehicle == null) return NotFound("Vozidlo nenájdené");

        // Overenie prístupu - majiteľ, admin, alebo servis s oprávnením
        bool hasAccess = user.Role == UserRole.Admin ||
                         vehicle.OwnerId == user.Id ||
                         await _context.Permissions.AnyAsync(p => p.VehicleId == vehicleId && p.ServiceId == user.Id && p.IsActive);

        if (!hasAccess) return Forbid();

        // Registrácia kódovania pre stredoeurópske znaky
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var memoryStream = new MemoryStream();
        var document = new Document(PageSize.A4, 40, 40, 40, 40);
        var writer = PdfWriter.GetInstance(document, memoryStream);

        document.Open();

        // Fonty
        var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
        var baseFontBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1250, BaseFont.EMBEDDED);
        var titleFont = new Font(baseFontBold, 22, Font.BOLD, new BaseColor(30, 58, 95));
        var subtitleFont = new Font(baseFontBold, 14, Font.BOLD, new BaseColor(37, 99, 235));
        var headerFont = new Font(baseFontBold, 11, Font.BOLD, new BaseColor(255, 255, 255));
        var normalFont = new Font(baseFont, 10, Font.NORMAL, new BaseColor(15, 23, 42));
        var smallFont = new Font(baseFont, 9, Font.NORMAL, new BaseColor(100, 116, 139));
        var labelFont = new Font(baseFontBold, 10, Font.BOLD, new BaseColor(100, 116, 139));
        var valueFont = new Font(baseFontBold, 11, Font.BOLD, new BaseColor(15, 23, 42));

        // === HLAVICKA ===
        var headerTable = new PdfPTable(2) { WidthPercentage = 100 };
        headerTable.SetWidths(new float[] { 70, 30 });

        var titleCell = new PdfPCell();
        titleCell.Border = Rectangle.NO_BORDER;
        titleCell.AddElement(new Paragraph("Servisna knizka", titleFont));
        titleCell.AddElement(new Paragraph("Kompletna historia servisnych zaznamov", smallFont));
        headerTable.AddCell(titleCell);

        var dateCell = new PdfPCell();
        dateCell.Border = Rectangle.NO_BORDER;
        dateCell.HorizontalAlignment = Element.ALIGN_RIGHT;
        dateCell.AddElement(new Paragraph($"Datum exportu:", smallFont) { Alignment = Element.ALIGN_RIGHT });
        dateCell.AddElement(new Paragraph($"{DateTime.Now:dd.MM.yyyy}", valueFont) { Alignment = Element.ALIGN_RIGHT });
        headerTable.AddCell(dateCell);

        document.Add(headerTable);
        document.Add(new Paragraph(" "));

        // === SEPARATOR ===
        var separatorTable = new PdfPTable(1) { WidthPercentage = 100 };
        var sepCell = new PdfPCell() { FixedHeight = 3, BackgroundColor = new BaseColor(37, 99, 235), Border = Rectangle.NO_BORDER };
        separatorTable.AddCell(sepCell);
        document.Add(separatorTable);
        document.Add(new Paragraph(" "));

        // === UDAJE O VOZIDLE ===
        document.Add(new Paragraph("Udaje o vozidle", subtitleFont));
        document.Add(new Paragraph(" "));

        var vehicleTable = new PdfPTable(4) { WidthPercentage = 100 };
        vehicleTable.SetWidths(new float[] { 25, 25, 25, 25 });

        AddInfoCell(vehicleTable, "Znacka", vehicle.Brand, labelFont, valueFont);
        AddInfoCell(vehicleTable, "Model", vehicle.Model, labelFont, valueFont);
        AddInfoCell(vehicleTable, "Rok vyroby", vehicle.Year.ToString(), labelFont, valueFont);
        AddInfoCell(vehicleTable, "SPZ", vehicle.LicensePlate, labelFont, valueFont);
        AddInfoCell(vehicleTable, "VIN", vehicle.VIN, labelFont, valueFont);
        AddInfoCell(vehicleTable, "Farba", vehicle.Color ?? "-", labelFont, valueFont);
        AddInfoCell(vehicleTable, "Motor", vehicle.EngineType ?? "-", labelFont, valueFont);
        AddInfoCell(vehicleTable, "Vykon", vehicle.EnginePower.HasValue ? $"{vehicle.EnginePower} kW" : "-", labelFont, valueFont);

        document.Add(vehicleTable);
        document.Add(new Paragraph(" "));

        // Majitel
        var ownerTable = new PdfPTable(2) { WidthPercentage = 100 };
        ownerTable.SetWidths(new float[] { 50, 50 });
        AddInfoCell(ownerTable, "Majitel", vehicle.Owner.FullName, labelFont, valueFont);
        AddInfoCell(ownerTable, "Email", vehicle.Owner.Email ?? "-", labelFont, valueFont);
        document.Add(ownerTable);
        document.Add(new Paragraph(" "));
        document.Add(new Paragraph(" "));

        // === SERVISNE ZAZNAMY ===
        var records = vehicle.ServiceRecords.OrderByDescending(r => r.ServiceDate).ToList();
        document.Add(new Paragraph($"Servisne zaznamy ({records.Count})", subtitleFont));
        document.Add(new Paragraph(" "));

        if (records.Any())
        {
            // Hlavicka tabulky
            var recordsTable = new PdfPTable(6) { WidthPercentage = 100 };
            recordsTable.SetWidths(new float[] { 14, 16, 28, 12, 15, 15 });

            var headerBg = new BaseColor(30, 58, 95);
            AddHeaderCell(recordsTable, "Datum", headerFont, headerBg);
            AddHeaderCell(recordsTable, "Typ", headerFont, headerBg);
            AddHeaderCell(recordsTable, "Popis prac", headerFont, headerBg);
            AddHeaderCell(recordsTable, "km", headerFont, headerBg);
            AddHeaderCell(recordsTable, "Cena", headerFont, headerBg);
            AddHeaderCell(recordsTable, "Servis", headerFont, headerBg);

            bool alternate = false;
            foreach (var record in records)
            {
                var bgColor = alternate ? new BaseColor(241, 245, 249) : new BaseColor(255, 255, 255);
                AddDataCell(recordsTable, record.ServiceDate.ToString("dd.MM.yyyy"), normalFont, bgColor);
                AddDataCell(recordsTable, record.ServiceType ?? "-", normalFont, bgColor);
                AddDataCell(recordsTable, record.Description, normalFont, bgColor);
                AddDataCell(recordsTable, record.Mileage.ToString("N0"), normalFont, bgColor);
                AddDataCell(recordsTable, record.Cost.HasValue ? $"{record.Cost.Value:N2} EUR" : "-", normalFont, bgColor);
                AddDataCell(recordsTable, record.CreatedBy?.FullName ?? "-", smallFont, bgColor);
                alternate = !alternate;
            }

            document.Add(recordsTable);
            document.Add(new Paragraph(" "));

            // Sumar
            var totalCost = records.Sum(r => r.Cost ?? 0);
            var lastMileage = records.OrderByDescending(r => r.ServiceDate).FirstOrDefault()?.Mileage ?? 0;
            
            document.Add(new Paragraph(" "));
            var summaryTable = new PdfPTable(3) { WidthPercentage = 100 };
            summaryTable.SetWidths(new float[] { 33, 33, 34 });
            AddInfoCell(summaryTable, "Celkovy pocet zaznamov", records.Count.ToString(), labelFont, valueFont);
            AddInfoCell(summaryTable, "Celkove naklady", $"{totalCost:N2} EUR", labelFont, valueFont);
            AddInfoCell(summaryTable, "Posledny stav km", $"{lastMileage:N0} km", labelFont, valueFont);
            document.Add(summaryTable);
        }
        else
        {
            document.Add(new Paragraph("Ziadne servisne zaznamy pre toto vozidlo.", normalFont));
        }

        // Paticka
        document.Add(new Paragraph(" "));
        document.Add(new Paragraph(" "));
        var footerSep = new PdfPTable(1) { WidthPercentage = 100 };
        var footerSepCell = new PdfPCell() { FixedHeight = 1, BackgroundColor = new BaseColor(226, 232, 240), Border = Rectangle.NO_BORDER };
        footerSep.AddCell(footerSepCell);
        document.Add(footerSep);
        document.Add(new Paragraph($"Vygenerovane: {DateTime.Now:dd.MM.yyyy HH:mm} | Servisna knizka", smallFont));

        document.Close();

        var fileName = $"Servisna_knizka_{vehicle.Brand}_{vehicle.Model}_{vehicle.LicensePlate}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(memoryStream.ToArray(), "application/pdf", fileName);
    }

    private static void AddInfoCell(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
    {
        var cell = new PdfPCell
        {
            Border = Rectangle.NO_BORDER,
            Padding = 8
        };
        cell.AddElement(new Paragraph(label, labelFont));
        cell.AddElement(new Paragraph(value, valueFont));
        table.AddCell(cell);
    }

    private static void AddHeaderCell(PdfPTable table, string text, Font font, BaseColor bgColor)
    {
        var cell = new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = bgColor,
            Padding = 8,
            Border = Rectangle.NO_BORDER,
            PaddingBottom = 10
        };
        table.AddCell(cell);
    }

    private static void AddDataCell(PdfPTable table, string text, Font font, BaseColor bgColor)
    {
        var cell = new PdfPCell(new Phrase(text, font))
        {
            BackgroundColor = bgColor,
            Padding = 7,
            Border = Rectangle.NO_BORDER,
            BorderWidthBottom = 0.5f,
            BorderColorBottom = new BaseColor(226, 232, 240)
        };
        table.AddCell(cell);
    }
}
