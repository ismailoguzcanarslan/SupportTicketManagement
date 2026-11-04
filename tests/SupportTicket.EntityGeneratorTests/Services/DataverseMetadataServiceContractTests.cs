
using SupportTicket.EntityGenerator.Services;
using Xunit;
using NSubstitute;
using Microsoft.Xrm.Sdk.Metadata;

namespace SupportTicket.EntityGeneratorTests.Services;

public class DataverseMetadataServiceContractTests
{
    [Fact]
    public async Task GetEntityMetadataAsync_ShouldReturnConfiguredMetadata()
    {
        // ARRANGE (Hazırlık)

        // 1. Gerçek Dataverse bağlantısı kullanmadan
        // IDataverseMetadataService için sahte servis oluşturuyoruz.
        var metadataService = Substitute.For<IDataverseMetadataService>();

        // 2. Testte kullanılacak örnek metadata nesnesini hazırlıyoruz.
        var expectedMetadata = new EntityMetadata
        {
            LogicalName = "ogz_support_ticket"
        };

        // 3. Mock davranışını belirliyoruz (Stubbing).
        // Bu tablo istendiğinde hazırladığımız metadata nesnesini döndür.
        metadataService.GetEntityMetadataAsync("ogz_support_ticket").Returns(Task.FromResult(expectedMetadata));


        // ACT (Çalıştırma)

        // 4. Mock servisin metodunu çağırıyoruz.
        // await ile Task tamamlandığında sonucu alıyoruz.
        var actualMetadata = await metadataService.GetEntityMetadataAsync("ogz_support_ticket");


        // ASSERT (Doğrulama)

        // 5. Dönen metadata nesnesi null olmamalı.
        Assert.NotNull(actualMetadata);

        // 6. Dönen tablonun LogicalName değeri beklediğimizle aynı olmalı.
        Assert.Equal(expectedMetadata.LogicalName, actualMetadata.LogicalName);

        // 7. Dönen nesne hazırladığımız nesnenin aynısı olmalı.
        Assert.Same(expectedMetadata, actualMetadata);

        // VERIFY (Çağrı doğrulama)
        // Metadata okuma metodu belirtilen tablo için
        // tam olarak bir kez çağrılmış olmalı.
        await metadataService.Received(1).GetEntityMetadataAsync("ogz_support_ticket");

    }
}
