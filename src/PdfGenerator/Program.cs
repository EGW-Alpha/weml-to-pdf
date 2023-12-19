using Egw.PubManagement.Persistence;
using Egw.PubManagement.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WemlToPdf;
using WemlToPdf.Services;
using WemlToPdf.Types;

var services = new ServiceCollection();
var configuration = new ConfigurationBuilder()
    .AddJsonFile("./appsettings.json") // add the rest of config sources
    .AddEnvironmentVariables()
    .Build();
services.AddLogging(o => o.AddConsole());

services.AddDbContextFactory<PublicationDbContext>(o => o.UseNpgsql(
        configuration.GetConnectionString("Publications"))
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors());

ServiceProvider serviceProvider = services.BuildServiceProvider();

var scope = serviceProvider.CreateScope();
var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PublicationDbContext>>();

var db = new PublicationDbContext(new DbContextOptions<PublicationDbContext>(), configuration.GetConnectionString("Publications"));

var storageWrapper = new StorageWrapper(
    new BlobFileStorage(
        configuration.GetConnectionString("Covers") ?? ""),
    new BlobFileStorage(
        configuration.GetConnectionString("Exports") ?? ""),
    new BlobFileStorage(
        configuration.GetConnectionString("Mp3") ?? ""));

var converter = new WemlToPdfConverterV2(db, new CoverFetcher(storageWrapper, dbContextFactory),
    serviceProvider.GetRequiredService<ILoggerFactory>());


await converter.CreatePdf(new WemlToPdfConfig
{
    PublicationId = 128, 
    FootnotesLevel = 3, 
    MaxChapterLevelInToc = 3, 
    PageSize = PageSizeEnum.A5,
    PrintType = PdfTargetType.BookPrinting,
    PageOrientation = PageOrientationEnum.Portrait,
    TocPosition = TocPositionEnum.AfterTitle,
    UseChapterPartTitle = true,
    OutputFolder = null,
    CreatePdfAfterHtmlGeneration = false
}, new CancellationToken());