using AutoMapper;
using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.BaseRequest;
using HCQS.BackEnd.Common.Dto.Record;
using HCQS.BackEnd.Common.Dto.Request;
using HCQS.BackEnd.Common.Dto.Response;
using HCQS.BackEnd.Common.Util;
using HCQS.BackEnd.DAL.Contracts;
using HCQS.BackEnd.DAL.Implementations;
using HCQS.BackEnd.DAL.Models;
using HCQS.BackEnd.Service.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.POIFS.Crypt.Dsig;
using OfficeOpenXml;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Transactions;

namespace HCQS.BackEnd.Service.Implementations
{
    public class SupplierPriceQuotationService : GenericBackendService, ISupplierPriceQuotationService
    {
        private BackEndLogger _logger;
        private IUnitOfWork _unitOfWork;
        private ISupplierPriceQuotationRepository _supplierPriceQuotationRepository;
        private IMapper _mapper;
        private IFileService _fileService;

        public SupplierPriceQuotationService(BackEndLogger logger, IMapper mapper, IUnitOfWork unitOfWork, ISupplierPriceQuotationRepository supplierPriceQuotationRepository, IFileService fileService, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _unitOfWork = unitOfWork;
            _supplierPriceQuotationRepository = supplierPriceQuotationRepository;
            _logger = logger;
            _mapper = mapper;
            _fileService = fileService;
        }

        public async Task<AppActionResult> CreateSupplierPriceQuotation(SupplierPriceQuotationRequest supplierPriceQuotationRequest)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    if (supplierPriceQuotationRequest.MaterialQuotationRecords != null && supplierPriceQuotationRequest.MaterialQuotationRecords.Count > 0)
                    {
                        var supplierRepository = Resolve<ISupplierRepository>();
                        var supplierDb = await supplierRepository.GetById(supplierPriceQuotationRequest.SupplierId);
                        if (supplierDb != null)
                        {
                            result = BuildAppActionResultError(result, $"The supplier whose id is {supplierPriceQuotationRequest.SupplierId} does not exist!");
                        }
                        else
                        {
                            var supplierPriceQuotation = _mapper.Map<SupplierPriceQuotation>(supplierPriceQuotationRequest);
                            supplierPriceQuotation.Id = Guid.NewGuid();
                            await _supplierPriceQuotationRepository.Insert(supplierPriceQuotation);
                            var records = supplierPriceQuotationRequest.MaterialQuotationRecords;
                            var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();
                            var supplierPriceDetails = await GetSupplierPriceDetailFromRecords(supplierPriceQuotation.Id, records, new Dictionary<string, Guid>(), Resolve<IMaterialRepository>());
                            if (supplierPriceDetails == null || supplierPriceDetails.Count == 0)
                            {
                                result = BuildAppActionResultError(result, $"Input Supplier price details are invalid");
                            }
                            else
                            {
                                await supplierPriceDetailRepository.InsertRange(supplierPriceDetails);
                            }
                            if (!BuildAppActionResultIsError(result))
                            {
                                await _unitOfWork.SaveChangeAsync();
                                result.Result.Data = new SupplierPriceQuotationResponse()
                                {
                                    SupplierPriceQuotation = supplierPriceQuotation,
                                    SupplierPriceDetails = supplierPriceDetails,
                                    Date = supplierPriceQuotation.Date
                                };
                            }
                        }
                    }
                    else
                    {
                        result = BuildAppActionResultError(result, $"There is no supplier quotation detail!");
                    }

                    if (!BuildAppActionResultIsError(result))
                    {
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    result = BuildAppActionResultError(result, ex.Message);
                    _logger.LogError(ex.Message, this);
                }
                return result;
            }
        }

        public async Task<AppActionResult> DeleteSupplierPriceQuotationById(Guid id)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var supplierPriceQuotation = await _supplierPriceQuotationRepository.GetById(id);
                    if (supplierPriceQuotation != null)
                    {
                        var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();
                        var supplierPriceDetails = await supplierPriceDetailRepository.GetAllDataByExpression(s => s.SupplierPriceQuotationId == id);
                        
                        if (supplierPriceDetails.Count > 0)
                        {
                            var supplierPriceDetailIds = supplierPriceDetails.Select(s => s.Id).ToList();
                            var importExportInventoryHistoryRepository = Resolve<IImportExportInventoryHistoryRepository>();

                            var inventoryRecords = await importExportInventoryHistoryRepository.GetAllDataByExpression(i => i.SupplierPriceDetailId != null && supplierPriceDetailIds.Contains((Guid)i.SupplierPriceDetailId));
                            if(inventoryRecords.Count == 0)
                            {
                                await supplierPriceDetailRepository.DeleteRange(supplierPriceDetails);
                                await _unitOfWork.SaveChangeAsync();
                                await _supplierPriceQuotationRepository.DeleteById(id);
                                await _unitOfWork.SaveChangeAsync();
                            }
                            else
                            {
                                result = BuildAppActionResultError(result, $"Unable to delete this quotation as there are materials imported with some of its price details!");
                            }

                        }
                        else
                        {
                            result = BuildAppActionResultError(result, $"There is no price detail in this quotation!");
                        }
                        
                    }
                    else
                    {
                        result = BuildAppActionResultError(result, $"Supplier prcie quotation with id: {id} does not exist!");
                    }

                    if (!BuildAppActionResultIsError(result))
                    {
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    result = BuildAppActionResultError(result, ex.Message);
                    _logger.LogError(ex.Message, this);
                }
                return result;
            }
        }

        public async Task<AppActionResult> GetAll(int pageIndex, int pageSize, IList<SortInfo> sortInfos)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var sampleList = await _supplierPriceQuotationRepository.GetAllDataByExpression(null, s => s.Supplier);
                List<SupplierPriceQuotationResponse> supplierQuotations = new List<SupplierPriceQuotationResponse>();
                var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();

                foreach (var sample in sampleList)
                {
                    List<SupplierPriceDetail> supplierPriceDetails = await supplierPriceDetailRepository.GetAllDataByExpression(S => S.SupplierPriceQuotationId == sample.Id, s => s.Material);

                    supplierQuotations.Add(

                        new SupplierPriceQuotationResponse
                        {
                            SupplierPriceQuotation = sample,
                            SupplierPriceDetails = supplierPriceDetails
                        });
                }

                var SD = Resolve<SD>();

                if (supplierQuotations.Any())
                {
                    if (pageIndex <= 0) pageIndex = 1;
                    if (pageSize <= 0) pageSize = SD.MAX_RECORD_PER_PAGE;
                    int totalPage = DataPresentationHelper.CalculateTotalPageSize(supplierQuotations.Count(), pageSize);

                    if (sortInfos != null)
                    {
                        supplierQuotations = DataPresentationHelper.ApplySorting(supplierQuotations, sortInfos);
                    }
                    if (pageIndex > 0 && pageSize > 0)
                    {
                        supplierQuotations = DataPresentationHelper.ApplyPaging(supplierQuotations, pageIndex, pageSize);
                    }
                    result.Result.Data = supplierQuotations;
                    result.Result.TotalPage = totalPage;
                }
                else
                {
                    result.Messages.Add("Empty sample project list");
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetQuotationByMonth(int month, int year, int pageIndex, int pageSize, IList<SortInfo> sortInfos)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var sampleList = await _supplierPriceQuotationRepository.GetAllDataByExpression(s => s.Date.Month == month && s.Date.Year == year, s => s.Supplier);
                List<SupplierPriceQuotationResponse> supplierQuotations = new List<SupplierPriceQuotationResponse>();
                var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();

                foreach (var sample in sampleList)
                {
                    List<SupplierPriceDetail> supplierPriceDetails = await supplierPriceDetailRepository.GetAllDataByExpression(S => S.SupplierPriceQuotationId == sample.Id, s => s.Material);

                    supplierQuotations.Add(

                        new SupplierPriceQuotationResponse
                        {
                            SupplierPriceQuotation = sample,
                            SupplierPriceDetails = supplierPriceDetails
                        });
                }

                var SD = Resolve<SD>();

                if (supplierQuotations.Any())
                {
                    if (pageIndex <= 0) pageIndex = 1;
                    if (pageSize <= 0) pageSize = SD.MAX_RECORD_PER_PAGE;
                    int totalPage = DataPresentationHelper.CalculateTotalPageSize(supplierQuotations.Count(), pageSize);

                    if (sortInfos != null)
                    {
                        supplierQuotations = DataPresentationHelper.ApplySorting(supplierQuotations, sortInfos);
                    }
                    if (pageIndex > 0 && pageSize > 0)
                    {
                        supplierQuotations = DataPresentationHelper.ApplyPaging(supplierQuotations, pageIndex, pageSize);
                    }
                    result.Result.Data = supplierQuotations;
                    result.Result.TotalPage = totalPage;
                }
                else
                {
                    result.Messages.Add("Empty sample project list");
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public Task<AppActionResult> UpdateSupplierPriceQuotation(SupplierPriceQuotationRequest supplierPriceQuotationRequest)
        {
            throw new NotImplementedException();
        }

        public async Task<IActionResult> UploadSupplierQuotationWithExcelFile(IFormFile file)
        {
            IActionResult result = new ObjectResult(null) { StatusCode = 200 };
            string message = "";
            if (file == null || file.Length == 0)
            {
                return result;
            }
            else
            {
                bool isSuccessful = true;
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        //Format: Name_ddmmyyy
                        //Format: ddMMyyyy
                        string nameDateString = file.FileName;
                        if (file.FileName.Contains("(ErrorColor)"))
                            nameDateString = nameDateString.Substring("(ErrorColor)".Length);
                        string[] supplierInfo = nameDateString.Split('_');
                        if (supplierInfo.Length != 2)
                        {
                            return new ObjectResult("Invalid file name. Please follow format: SupplierName_ddMMyyyy") { StatusCode = 200 };
                        }
                        string supplierName = supplierInfo[0];
                        if (supplierInfo[1].Length < 8)
                            return new ObjectResult("Invalid date. Please follow date format: ddMMyyyy") { StatusCode = 200 };
                        string dateString = supplierInfo[1].Substring(0, 8);
                        if (!DateTime.TryParseExact(dateString, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                        {
                            isSuccessful = false;
                            _logger.LogError($"{dateString} is not in format: ddMMyyyy", this);
                            message = $"{dateString} is not in format: ddMMyyyy";
                        }
                        else
                        {
                            string errorHeader = await CheckHeader(file, SD.ExcelHeaders.SUPPLIER_QUOTATION_DETAIL);
                            if (!string.IsNullOrEmpty(errorHeader))
                            {
                                isSuccessful = false;
                                _logger.LogError(errorHeader, this);
                                message = errorHeader;
                            }
                            else
                            {
                                var supplierRepository = Resolve<ISupplierRepository>();
                                var supplier = await supplierRepository.GetByExpression(s => s.SupplierName.ToLower().Equals(supplierName.ToLower()) && !s.IsDeleted);
                                if (supplier == null)
                                {
                                    _logger.LogError($"Supplier with name: {supplierName} does not exist!", this);
                                    message = $"Supplier with name: {supplierName} does not exist!";
                                    isSuccessful = false;
                                }
                                if (isSuccessful)
                                {
                                    SupplierPriceQuotation newSupplierPriceQuotation = new SupplierPriceQuotation()
                                    {
                                        Id = Guid.NewGuid(),
                                        Date = date,
                                        SupplierId = supplier.Id
                                    };
                                    
                                    Dictionary<String, Guid> materials = new Dictionary<String, Guid>();
                                    Dictionary<string, int> duplicatedQuotation = new Dictionary<string, int>();
                                    List<SupplierMaterialQuotationRecord> records = await GetListFromExcel(file);
                                    if (records.Count == 0)
                                    {
                                        return new ObjectResult("Empty record list!") { StatusCode = 200 };
                                    }
                                    await _supplierPriceQuotationRepository.Insert(newSupplierPriceQuotation);
                                    await _unitOfWork.SaveChangeAsync();
                                    List<SupplierPriceDetail> supplierPriceDetails = new List<SupplierPriceDetail>();
                                    var materialRepository = Resolve<IMaterialRepository>();
                                    var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();
                                    Dictionary<int, string> invalidRowInput = new Dictionary<int, string>();
                                    int errorRecordCount = 0;
                                    SD.EnumType.SupplierType.TryGetValue(supplier.Type.ToString(), out int supplierType);
                                    int i = 2;
                                    string key = "";
                                    foreach (SupplierMaterialQuotationRecord record in records)
                                    {
                                        StringBuilder error = new StringBuilder();
                                        errorRecordCount = 0;
                                        Guid materialId = Guid.Empty;
                                        if (string.IsNullOrEmpty(record.MaterialName) || string.IsNullOrEmpty(record.Unit))
                                        {
                                            error.Append($"{errorRecordCount + 1}. Material Name or Unit cell is empty.\n");
                                            errorRecordCount++;
                                        }
                                        else
                                        {
                                            SD.EnumType.MaterialUnit.TryGetValue(record.Unit, out int materialUnit);
                                            key = record.MaterialName + '-' + record.Unit;
                                            if (supplierType == 2 || (materialUnit < 3 && supplierType == 0) || (materialUnit == 3 && supplierType == 1))
                                            {
                                                
                                                if (materials.ContainsKey(key)) materialId = materials[key];
                                                else
                                                {
                                                    bool containsUnit = SD.EnumType.MaterialUnit.TryGetValue(record.Unit, out int index);
                                                    if (containsUnit)
                                                    {
                                                        var material = await materialRepository.GetByExpression(m => m.Name.Equals(record.MaterialName) && (int)(m.UnitMaterial) == index);
                                                        if (material == null)
                                                        {
                                                            error.Append($"{errorRecordCount + 1}. Material with name: {record.MaterialName} and unit: {record.Unit} does not exist.\n");
                                                            errorRecordCount++;
                                                        }
                                                        else
                                                        {
                                                            materialId = material.Id;
                                                            materials.Add(key, materialId);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        error.Append($"{errorRecordCount + 1}. Unit: {record.Unit} does not exist.\n");
                                                        errorRecordCount++;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                error.Append($"{errorRecordCount + 1}. Supplier {supplierName} is a {supplier.Type.ToString()} so they don't supply {record.MaterialName}.\n");
                                                errorRecordCount++;
                                            }
                                        }

                                        if (record.MOQ <= 0)
                                        {
                                            error.Append($"{errorRecordCount + 1}. MOQ(Minimum Order Quantity) must be higher than 0.\n");
                                            errorRecordCount++;
                                        }

                                        if (record.Price <= 0)
                                        {
                                            error.Append($"{errorRecordCount + 1}. Price must be higher than 0.\n");
                                            errorRecordCount++;
                                        }
                                        string duplicatedKey = $"{record.MaterialName}-{record.MOQ}";
                                        if (duplicatedQuotation.ContainsKey(duplicatedKey))
                                        {
                                            error.Append($"{errorRecordCount + 1}. Duplicated material name and MOQ with row {duplicatedQuotation[duplicatedKey]}.\n");
                                            errorRecordCount++;
                                        }
                                        else
                                        {
                                            duplicatedQuotation.Add(duplicatedKey, i - 1);
                                        }

                                        if (materials.ContainsKey(key) && record.MOQ > 0)
                                        {
                                            if ((await supplierPriceDetailRepository.GetAllDataByExpression(s => s.SupplierPriceQuotation.SupplierId == supplier.Id
                                                                                                            && s.SupplierPriceQuotation.Date.DayOfYear == date.DayOfYear
                                                                                                            && s.SupplierPriceQuotation.Date.Year == date.Year
                                                                                                            && s.MaterialId == materialId
                                                                                                            && s.MOQ == record.MOQ)).Count > 0)
                                            {
                                                error.Append($"{errorRecordCount + 1}.(Warning) There exists material price quotation of supplier {supplierName} at {date.Date} with the same MOQ.\n");
                                                errorRecordCount++;
                                            }
                                        }

                                        if (errorRecordCount == 0)
                                        {
                                            var newPriceDetail = new SupplierPriceDetail()
                                            {
                                                Id = Guid.NewGuid(),
                                                MaterialId = materialId,
                                                MOQ = int.Parse(record.MOQ.ToString()),
                                                Price = record.Price,
                                                SupplierPriceQuotationId = newSupplierPriceQuotation.Id
                                            };

                                            await supplierPriceDetailRepository.Insert(newPriceDetail);
                                            supplierPriceDetails.Add(newPriceDetail);
                                        }
                                        else
                                        {
                                            error.Append("(Please delete this error message cell before re-uploading!)");
                                            invalidRowInput.Add(i, error.ToString());
                                        }
                                        i++;
                                    }

                                    if (invalidRowInput.Count > 0)
                                    {
                                        List<List<string>> recordDataString = new List<List<string>>();
                                        int j = 1;
                                        foreach (var record in records)
                                        {
                                            recordDataString.Add(new List<string>
                                        {
                                            j++.ToString(), record.MaterialName, record.Unit, record.MOQ.ToString(), record.Price.ToString()
                                        });
                                        }
                                        result = _fileService.ReturnErrorColored<SupplierMaterialQuotationRecord>(SD.ExcelHeaders.SUPPLIER_QUOTATION_DETAIL, recordDataString, invalidRowInput, file.FileName);
                                        isSuccessful = false;
                                        _logger.LogError($"Invalid rows are colored in the excel file!", this);
                                    }

                                    if (isSuccessful)
                                    {
                                        await _unitOfWork.SaveChangeAsync();
                                        result = new ObjectResult(new SupplierPriceQuotationResponse()
                                        {
                                            SupplierPriceQuotation = newSupplierPriceQuotation,
                                            SupplierPriceDetails = supplierPriceDetails,
                                            Date = newSupplierPriceQuotation.Date
                                        })
                                        { StatusCode = 200 };
                                    }
                                }
                            }

                            if (isSuccessful)
                            {
                                scope.Complete();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message, this);
                    }
                }
            }
            if (!string.IsNullOrEmpty(message)) return new ObjectResult(message) { StatusCode = 200 };
            return result;
        }

        private async Task<List<SupplierMaterialQuotationRecord>> GetListFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (ExcelPackage package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0]; // Assuming data is in the first sheet

                        int rowCount = worksheet.Dimension.Rows;
                        int colCount = worksheet.Dimension.Columns;

                        List<SupplierMaterialQuotationRecord> records = new List<SupplierMaterialQuotationRecord>();

                        for (int row = 2; row <= rowCount; row++) // Assuming header is in the first row
                        {
                            SupplierMaterialQuotationRecord record = new SupplierMaterialQuotationRecord()
                            {
                                Id = Guid.NewGuid(),
                                No = (worksheet.Cells[row, 1].Value == null) ? 0 : int.Parse(worksheet.Cells[row, 1].Value.ToString()),
                                MaterialName = (worksheet.Cells[row, 2].Value == null) ? "" : worksheet.Cells[row, 2].Value.ToString(),
                                Unit = (worksheet.Cells[row, 3].Value == null) ? "" : worksheet.Cells[row, 3].Value.ToString(),
                                MOQ = (worksheet.Cells[row, 4].Value == null) ? 0 : int.Parse(worksheet.Cells[row, 4].Value.ToString()),
                                Price = (worksheet.Cells[row, 5].Value == null) ? 0 : double.Parse(worksheet.Cells[row, 5].Value.ToString())
                            };
                            records.Add(record);
                        }
                        return records;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, this);
            }
            return null;
        }

        private async Task<string> CheckHeader(IFormFile file, List<string> headerTemplate)
        {
            if (file == null || file.Length == 0)
            {
                return "File not found";
            }

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (ExcelPackage package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0]; // Assuming data is in the first sheet

                        int colCount = worksheet.Columns.Count();
                        if (colCount != headerTemplate.Count && worksheet.Cells[1, colCount].Value != null)
                        {
                            return "Difference in column names";
                        }
                        StringBuilder sb = new StringBuilder();
                        sb.Append("Incorrect column names: ");
                        bool containsError = false;
                        for (int col = 1; col <= Math.Min(5, worksheet.Columns.Count()); col++) // Assuming header is in the first row
                        {
                            if (!worksheet.Cells[1, col].Value.Equals(headerTemplate[col - 1]))
                            {
                                if(!containsError) containsError = true;
                                sb.Append($"{worksheet.Cells[1, col].Value}(Correct: {headerTemplate[col - 1]}), ");
                            }
                        }
                        if (containsError)
                        {
                            return sb.Remove(sb.Length - 2, 2).ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, this);
            }
            return string.Empty;
        }

        private async Task<List<SupplierPriceDetail>> GetSupplierPriceDetailFromRecords(Guid supplierPriceQuotationId, List<SupplierMaterialQuotationRecord> records, Dictionary<String, Guid> materials, IMaterialRepository materialRepository)
        {
            if (records == null || records.Count < 1) return null;
            List<SupplierPriceDetail> supplierPriceDetails = new List<SupplierPriceDetail>();
            foreach (SupplierMaterialQuotationRecord record in records)
            {
                Guid materialId = Guid.Empty;
                if (materials.ContainsKey(record.MaterialName)) materialId = materials[record.MaterialName];
                else
                {
                    var material = await materialRepository.GetByExpression(m => m.Name.Equals(record.MaterialName));
                    if (material == null)
                    {
                        return null;
                    }
                    else
                    {
                        materialId = material.Id;
                        materials.Add(record.MaterialName, materialId);
                    }
                }

                var newPriceDetail = new SupplierPriceDetail()
                {
                    Id = Guid.NewGuid(),
                    MaterialId = materialId,
                    MOQ = int.Parse(record.MOQ.ToString()),
                    Price = record.Price,
                    SupplierPriceQuotationId = supplierPriceQuotationId
                };

                supplierPriceDetails.Add(newPriceDetail);
            }
            return supplierPriceDetails;
        }

        public async Task<IActionResult> GetPriceQuotationTemplate()
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                IActionResult result = null;
                try
                {
                    List<SupplierMaterialQuotationRecordSample> sampleData = new List<SupplierMaterialQuotationRecordSample>();
                    sampleData.Add(new SupplierMaterialQuotationRecordSample
                    {MaterialName = "Brick", Unit = "Bar", MOQ = 1000, Price = 9 });
                    result = _fileService.GenerateExcelContent<SupplierMaterialQuotationRecordSample>(sampleData, "SupplierPriceQuotationTemplate_Format_SupplierName_ddMMyyyy");
                    if (result != null)
                    {
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, this);
                }
                return result;
            }
        }

        public async Task<AppActionResult> ValidateExcelFile(IFormFile file)
        {
            AppActionResult result = new AppActionResult();
            ExcelValidatingResponse data = new ExcelValidatingResponse();
            if (file == null || file.Length == 0)
            {
                data.IsValidated = false;
                data.HeaderError = "Unable to validate excel file. Please restart!";
                result.Result.Data = data;
                return result;
            }
                try
                {
                    //Format: Name_ddmmyyy
                    //Format: ddMMyyyy
                    string nameDateString = file.FileName;
                    if (file.FileName.Contains("(ErrorColor)"))
                        nameDateString = nameDateString.Substring("(ErrorColor)".Length);
                    string[] supplierInfo = nameDateString.Split('_');
                    if (supplierInfo.Length != 2)
                    {
                        data.IsValidated = false;
                        data.HeaderError = "Invalid file name. Please follow format: SupplierName_ddMMyyyy";
                        result.Result.Data = data;
                        return result;
                }
                    string supplierName = supplierInfo[0];
                    if (supplierInfo[1].Length < 8)
                    {
                        data.IsValidated = false;
                        data.HeaderError = "Invalid date. Please follow date format: ddMMyyyy";
                        result.Result.Data = data;
                        return result;
                }

                    string dateString = supplierInfo[1].Substring(0, 8);
                    if (!DateTime.TryParseExact(dateString, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        data.IsValidated = false;
                        data.HeaderError = $"{dateString} is not in format: ddMMyyyy";
                        result.Result.Data = data;
                        return result;
                }
                    string errorHeader = await CheckHeader(file, SD.ExcelHeaders.SUPPLIER_QUOTATION_DETAIL);
                    if (!string.IsNullOrEmpty(errorHeader))
                    {
                        data.IsValidated = false;
                        data.HeaderError = errorHeader;
                        result.Result.Data = data;
                        return result;
                    }
                    var supplierRepository = Resolve<ISupplierRepository>();
                    var supplier = await supplierRepository.GetByExpression(s => s.SupplierName.ToLower().Equals(supplierName.ToLower()) && !s.IsDeleted);
                    if (supplier == null)
                    {
                        data.IsValidated = false;
                        data.HeaderError = $"Supplier with name: {supplierName} does not exist!";
                        result.Result.Data = data;
                        return result;
                    }                    

                    Dictionary<string, Guid> materials = new Dictionary<string, Guid>();
                    Dictionary<string, int> duplicatedQuotation = new Dictionary<string, int>();
                    List<SupplierMaterialQuotationRecord> records = await GetListFromExcel(file);
                    if (records.Count == 0)
                    {
                        data.IsValidated = false;
                        data.HeaderError = $"Empty Record List";
                        result.Result.Data = data;
                        return result;
                    }
                var materialRepository = Resolve<IMaterialRepository>();
                    var supplierPriceQuotationDetailRepository = Resolve<ISupplierPriceDetailRepository>();
                    int errorRecordCount = 0;
                    SD.EnumType.SupplierType.TryGetValue(supplier.Type.ToString(), out int supplierType);
                    int i = 2;
                    int invalidRowInput = 0;
                    string key = "";
                    data.Errors = new string[records.Count];
                    foreach (SupplierMaterialQuotationRecord record in records)
                    {
                        StringBuilder error = new StringBuilder();
                        errorRecordCount = 0;
                        Guid materialId = Guid.Empty;
                        if(record.No != i- 1)
                        {
                            error.Append($"{errorRecordCount + 1}. No should be {i-1}.\n");
                            errorRecordCount++;
                        }

                        if (string.IsNullOrEmpty(record.MaterialName) || string.IsNullOrEmpty(record.Unit))
                        {
                            error.Append($"{errorRecordCount + 1}. Material Name or Unit cell is empty.\n");
                            errorRecordCount++;
                        }
                        else
                        {
                            SD.EnumType.MaterialUnit.TryGetValue(record.Unit, out int materialUnit);
                        key = record.MaterialName + '-' + record.Unit;
                        if (supplierType == 2 || (materialUnit < 3 && supplierType == 0) || (materialUnit == 3 && supplierType == 1))
                            {
                                
                                if (materials.ContainsKey(key))  materialId = materials[key];
                                else
                                {
                                    bool containsUnit = SD.EnumType.MaterialUnit.TryGetValue(record.Unit, out int index);
                                    if (containsUnit)
                                    {
                                        var material = await materialRepository.GetByExpression(m => m.Name.Equals(record.MaterialName) && (int)(m.UnitMaterial) == index);
                                        if (material == null)
                                        {
                                            error.Append($"{errorRecordCount + 1}. Material with name: {record.MaterialName} and unit: {record.Unit} does not exist.\n");
                                            errorRecordCount++;
                                        }
                                        else
                                        {
                                            materialId = material.Id;
                                            materials.Add(key, materialId);   
                                        }
                                    }
                                    else
                                    {
                                        error.Append($"{errorRecordCount + 1}. Unit: {record.Unit} does not exist.\n");
                                        errorRecordCount++;
                                    }
                                }
                            }
                            else
                            {
                                error.Append($"{errorRecordCount + 1}. Supplier {supplierName} is a {supplier.Type.ToString()} so they don't supply {record.MaterialName}.\n");
                                errorRecordCount++;
                            }
                        }

                        if (record.MOQ <= 0)
                        {
                            error.Append($"{errorRecordCount + 1}. MOQ(Minimum Order Quantity) must be higher than 0.\n");
                            errorRecordCount++;
                        }

                        if (record.Price <= 0)
                        {
                            error.Append($"{errorRecordCount + 1}. Price must be higher than 0.\n");
                            errorRecordCount++;
                        }

                        string duplicatedKey = $"{record.MaterialName}-{record.MOQ}";
                        if (duplicatedQuotation.ContainsKey(duplicatedKey))
                        {
                            error.Append($"{errorRecordCount + 1}. Duplicated material name and MOQ with row {duplicatedQuotation[duplicatedKey]}.\n");
                            errorRecordCount++;
                        }
                        else
                        {
                        duplicatedQuotation.Add(duplicatedKey, i - 1);
                        }

                    if (materials.ContainsKey(key) && record.MOQ > 0)
                    {
                        if ((await supplierPriceQuotationDetailRepository.GetAllDataByExpression(s => s.SupplierPriceQuotation.SupplierId == supplier.Id 
                                                                                        && s.SupplierPriceQuotation.Date.DayOfYear == date.DayOfYear 
                                                                                        && s.SupplierPriceQuotation.Date.Year == date.Year
                                                                                        && s.MaterialId == materialId 
                                                                                        && s.MOQ == record.MOQ)).Count > 0)
                        {
                            error.Append($"{errorRecordCount + 1}.(Warning) There exists material price quotation of supplier {supplierName} at {date.Date} with the same MOQ.\n");
                            errorRecordCount++;
                        }
                    }
                   
                        if (errorRecordCount != 0)
                        {
                            data.Errors[i - 2] = error.ToString();
                            invalidRowInput++;
                        }
                        i++;
                    }

                    if (invalidRowInput > 0)
                    {
                        data.IsValidated = false;
                        result.Result.Data = data;
                        return result;
                    }

                    data.IsValidated = true;
                    data.Errors = null;
                    data.HeaderError = null;
                    result.Result.Data = data;
            }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, this);
                    data.IsValidated = false;
                }
            return result;
            
        }

        
    }
}