﻿using AutoMapper;
using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.BaseRequest;
using HCQS.BackEnd.Common.Dto.Record;
using HCQS.BackEnd.Common.Dto.Request;
using HCQS.BackEnd.DAL.Contracts;
using HCQS.BackEnd.DAL.Implementations;
using HCQS.BackEnd.DAL.Models;
using HCQS.BackEnd.DAL.Util;
using HCQS.BackEnd.Service.Contracts;
using HCQS.BackEnd.Service.Dto;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using System.Globalization;
using System.Transactions;

namespace HCQS.BackEnd.Service.Implementations
{
    public class SupplierPriceQuotationService : GenericBackendService, ISupplierPriceQuotationService
    {
        private BackEndLogger _logger;
        private IUnitOfWork _unitOfWork;
        private ISupplierPriceQuotationRepository _supplierPriceQuotationRepository;
        private IMapper _mapper;

        public SupplierPriceQuotationService(BackEndLogger logger, IMapper mapper, IUnitOfWork unitOfWork, ISupplierPriceQuotationRepository supplierPriceQuotationRepository, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _unitOfWork = unitOfWork;
            _supplierPriceQuotationRepository = supplierPriceQuotationRepository;
            _logger = logger;
            _mapper = mapper;
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
                    result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
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
                    var supplierPriceQuotation = _supplierPriceQuotationRepository.GetById(id);
                    if (supplierPriceQuotation != null)
                    {
                        var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();
                        var supplierPriceDetails = await supplierPriceDetailRepository.GetAllDataByExpression(s => s.SupplierPriceQuotationId == id);
                        await _supplierPriceQuotationRepository.DeleteById(id);
                        if (supplierPriceDetails != null)
                        {
                            await supplierPriceDetailRepository.DeleteRange(supplierPriceDetails);
                        }
                        await _unitOfWork.SaveChangeAsync();
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
                    result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
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
                var sampleList = await _supplierPriceQuotationRepository.GetAllDataByExpression(null, null);
                List<SupplierPriceQuotationResponse> supplierQuotations = new List<SupplierPriceQuotationResponse>();
                var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();

                foreach (var sample in sampleList)
                {
                    List<SupplierPriceDetail> supplierPriceDetails = await supplierPriceDetailRepository.GetAllDataByExpression(S => S.SupplierPriceQuotationId == sample.Id, null);

                    supplierQuotations.Add(

                        new SupplierPriceQuotationResponse
                        {
                            SupplierPriceQuotation = sample,
                            SupplierPriceDetails = supplierPriceDetails
                        });
                }

                var SD = Resolve<HCQS.BackEnd.DAL.Util.SD>();

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
                result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetQuotationByMonth(int month, int year, int pageIndex, int pageSize, IList<SortInfo> sortInfos)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var sampleList = await _supplierPriceQuotationRepository.GetAllDataByExpression(s => s.Date.Month == month && s.Date.Year == year, null);
                List<SupplierPriceQuotationResponse> supplierQuotations = new List<SupplierPriceQuotationResponse>();
                var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();

                foreach (var sample in sampleList)
                {
                    List<SupplierPriceDetail> supplierPriceDetails = await supplierPriceDetailRepository.GetAllDataByExpression(S => S.SupplierPriceQuotationId == sample.Id, null);

                    supplierQuotations.Add(

                        new SupplierPriceQuotationResponse
                        {
                            SupplierPriceQuotation = sample,
                            SupplierPriceDetails = supplierPriceDetails
                        });
                }

                var SD = Resolve<HCQS.BackEnd.DAL.Util.SD>();

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
                result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public Task<AppActionResult> UpdateSupplierPriceQuotation(SupplierPriceQuotationRequest supplierPriceQuotationRequest)
        {
            throw new NotImplementedException();
        }

        public async Task<AppActionResult> UploadSupplierQuotationWithExcelFile(IFormFile file)
        {
            AppActionResult result = new AppActionResult();
            if (file == null || file.Length == 0)
            {
                result.Result.Data = null;
                result.Messages.Add("Empty Excel file");
            }
            else
            {
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        //Format: Name_ddmmyyy
                        string[] supplierInfo = file.FileName.Split('_');
                        string supplierName = supplierInfo[0];
                        string dateString = supplierInfo[1].Substring(0, 8);
                        if (!DateTime.TryParseExact(dateString, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                        {
                            result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
                            _logger.LogError($"{dateString} is not in format: ddMMyyyy", this);
                        }
                        else
                        {
                            var supplierRepository = Resolve<ISupplierRepository>();
                            var supplier = await supplierRepository.GetByExpression(s => s.SupplierName.ToLower().Equals(supplierName.ToLower()));
                            if (supplier == null)
                            {
                                result = BuildAppActionResultError(result, $"Supplier with name: {supplierName} does not exist!");
                            }
                            if (!BuildAppActionResultIsError(result))
                            {
                                SupplierPriceQuotation newSupplierPriceQuotation = new SupplierPriceQuotation()
                                {
                                    Id = Guid.NewGuid(),
                                    Date = date,
                                    SupplierId = supplier.Id
                                };
                                await _supplierPriceQuotationRepository.Insert(newSupplierPriceQuotation);
                                await _unitOfWork.SaveChangeAsync();
                                Dictionary<String, Guid> materials = new Dictionary<String, Guid>();
                                List<SupplierMaterialQuotationRecord> records = await GetListFromExcel(file);
                                List<SupplierPriceDetail> supplierPriceDetails = new List<SupplierPriceDetail>();
                                var materialRepository = Resolve<IMaterialRepository>();
                                var supplierPriceDetailRepository = Resolve<ISupplierPriceDetailRepository>();
                                List<int> invalidRowInput = new List<int>();
                                int i = 2;
                                foreach (SupplierMaterialQuotationRecord record in records)
                                {
                                    Guid materialId = Guid.Empty;
                                    if (materials.ContainsKey(record.MaterialName)) materialId = materials[record.MaterialName];
                                    else
                                    {
                                        var material = await materialRepository.GetByExpression(m => m.Name.Equals(record.MaterialName));
                                        if (material == null)
                                        {
                                            // Color red the cell :vv
                                            invalidRowInput.Add(i);
                                        }
                                        else
                                        {
                                            materialId = material.Id;
                                            materials.Add(record.MaterialName, materialId);
                                        }
                                    }

                                    if(invalidRowInput.Count == 0)
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
                                    i++;
                                }

                                if(invalidRowInput.Count > 0)
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
                                    ExcelExporter.ExportToExcel(SD.ExcelHeaders.SUPPLIER_QUOTATION_DETAIL, recordDataString, invalidRowInput, ExcelExporter.GetDownloadsPath(file.FileName));
                                    result = BuildAppActionResultError(result, $"Invalid rows are colored in the excel file!");
                                }

                                if (!BuildAppActionResultIsError(result))
                                {
                                    await _unitOfWork.SaveChangeAsync();
                                    result.Result.Data = new SupplierPriceQuotationResponse()
                                    {
                                        SupplierPriceQuotation = newSupplierPriceQuotation,
                                        SupplierPriceDetails = supplierPriceDetails,
                                        Date = newSupplierPriceQuotation.Date
                                    };
                                }
                            }

                            if (!BuildAppActionResultIsError(result))
                            {
                                scope.Complete();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
                        _logger.LogError(ex.Message, this);
                    }
                }
            }
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
                                MaterialName = worksheet.Cells[row, 2].Value.ToString().ToString(),
                                Unit = worksheet.Cells[row, 3].Value.ToString().ToString(),
                                MOQ = int.Parse(worksheet.Cells[row, 4].Value.ToString().ToString()),
                                Price = double.Parse(worksheet.Cells[row, 5].Value.ToString().ToString())
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
    }
}