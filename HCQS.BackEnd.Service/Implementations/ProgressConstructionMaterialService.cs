﻿using AutoMapper;
using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.BaseRequest;
using HCQS.BackEnd.Common.Dto.Request;
using HCQS.BackEnd.Common.Util;
using HCQS.BackEnd.DAL.Contracts;
using HCQS.BackEnd.DAL.Models;
using HCQS.BackEnd.Service.Contracts;
using System.Transactions;

namespace HCQS.BackEnd.Service.Implementations
{
    public class ProgressConstructionMaterialService : GenericBackendService, IProgressConstructionMaterialService
    {
        private BackEndLogger _logger;
        private IUnitOfWork _unitOfWork;
        private IProgressConstructionMaterialRepository _progressConstructionMaterialRepository;
        private IMapper _mapper;

        public ProgressConstructionMaterialService(BackEndLogger logger, IUnitOfWork unitOfWork, IProgressConstructionMaterialRepository progressConstructionMaterialRepository, IMapper mapper, IServiceProvider service) : base(service)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _progressConstructionMaterialRepository = progressConstructionMaterialRepository;
            _mapper = mapper;
        }

        public async Task<AppActionResult> CreateProgressConstructionMaterial(List<ProgressConstructionMaterialRequest> ProgressConstructionMaterialRequests)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var quotationDetailRepository = Resolve<IQuotationDetailRepository>();
                    var exportPriceRepository = Resolve<IExportPriceMaterialRepository>();
                    Dictionary<Guid, int> materialExport = new Dictionary<Guid, int>();
                    List<ProgressConstructionMaterial> progressConstructionMaterials = new List<ProgressConstructionMaterial>();
                    foreach (var ProgressConstructionMaterialRequest in ProgressConstructionMaterialRequests)
                    {
                        //get material => get latest price =>
                        var quotationDetailDb = await quotationDetailRepository.GetByExpression(q => q.Id == ProgressConstructionMaterialRequest.QuotationDetailId, q => q.Material);
                        if (quotationDetailDb != null)
                        {
                            var quotationRepository = Resolve<IQuotationRepository>();
                            var quotationDb = await quotationRepository.GetByExpression(q => q.Id == quotationDetailDb.QuotationId && q.QuotationStatus == Quotation.Status.Approved, q => q.Project, q => q.Project.Contract);
                            if (quotationDb != null && quotationDb.Project.Status == Project.ProjectStatus.UnderConstruction && quotationDb.Project.Contract != null && quotationDb.Project.Contract.ContractStatus == Contract.Status.ACTIVE)
                            {
                                var contractProgressPaymentRepository = Resolve<IContractProgressPaymentRepository>();
                                var contractProgressPaymentDb = await contractProgressPaymentRepository.GetByExpression(c => c.ContractId == quotationDb.Project.Contract.Id && c.Name.Equals("Deposit"), c => c.Payment);
                                if (contractProgressPaymentDb != null)
                                {
                                    if (contractProgressPaymentDb.Payment.PaymentStatus == Payment.Status.Success)
                                    {
                                        var progressConstructionDb = await _progressConstructionMaterialRepository.GetAllDataByExpression(p => p.QuotationDetailId == ProgressConstructionMaterialRequest.QuotationDetailId);

                                        int remain = quotationDetailDb.Quantity;
                                        progressConstructionDb.ForEach(p => remain -= p.Quantity);
                                        if (remain < ProgressConstructionMaterialRequest.Quantity)
                                        {
                                            result = BuildAppActionResultError(result, $"The fulfilling quantity request is higher than remain quantity!");
                                        }
                                        else
                                        {
                                            var exportMaterialPrices = await exportPriceRepository.GetAllDataByExpression(e => e.MaterialId == quotationDetailDb.MaterialId, e => e.Material);
                                            var latestMaterialPrice = exportMaterialPrices.OrderByDescending(e => e.Date).FirstOrDefault();
                                            if (latestMaterialPrice != null)
                                            {
                                                var discount = await GetDiscountByQuotationDetailId(quotationDetailDb.Id);
                                                var utility = Resolve<Utility>();
                                                if (materialExport.ContainsKey(latestMaterialPrice.Material.Id))
                                                {
                                                    materialExport[latestMaterialPrice.Material.Id] += ProgressConstructionMaterialRequest.Quantity;
                                                }
                                                else
                                                {
                                                    materialExport.Add(latestMaterialPrice.Material.Id, ProgressConstructionMaterialRequest.Quantity);
                                                }
                                                var newProgressConstructionMaterial = new ProgressConstructionMaterial
                                                {
                                                    Id = Guid.NewGuid(),
                                                    Discount = discount,
                                                    Date = utility.GetCurrentDateTimeInTimeZone(),
                                                    Quantity = ProgressConstructionMaterialRequest.Quantity,
                                                    Total = ProgressConstructionMaterialRequest.Quantity * (1 - discount * 1.00) * latestMaterialPrice.Price,

                                                    ExportPriceMaterialId = latestMaterialPrice.Id,
                                                    QuotationDetailId = quotationDetailDb.Id
                                                };
                                                progressConstructionMaterials.Add(newProgressConstructionMaterial);
                                            }
                                            else
                                            {
                                                result = BuildAppActionResultError(result, $"There is no available material export price of {quotationDetailDb.Material.Name}!");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        result = BuildAppActionResultError(result, $"Project deposit has not been paid");
                                    }
                                }
                                else
                                {
                                    result = BuildAppActionResultError(result, $"There is no signed cotract including quotation detail: {ProgressConstructionMaterialRequest.QuotationDetailId} does not have a deposit!");
                                }
                            }
                            else
                            {
                                result = BuildAppActionResultError(result, $"There is no approved quotation, project or signed cotract including quotation detail: {ProgressConstructionMaterialRequest.QuotationDetailId} does not exist!");
                            }
                        }
                        else
                        {
                            result = BuildAppActionResultError(result, $"The quotation detail with id {ProgressConstructionMaterialRequest.QuotationDetailId} does not exist!");
                        }
                    }

                    if (!BuildAppActionResultIsError(result))
                    {
                        await _progressConstructionMaterialRepository.InsertRange(progressConstructionMaterials);
                        await _unitOfWork.SaveChangeAsync();
                        var importExportInventoryRepository = Resolve<IImportExportInventoryHistoryRepository>();
                        List<ImportExportInventoryHistory> importExportInventoryHistories = new List<ImportExportInventoryHistory>();
                        foreach (var progressConstructionMaterial in progressConstructionMaterials)
                        {
                            importExportInventoryHistories.Add(new ImportExportInventoryHistory
                            {
                                Id = Guid.NewGuid(),
                                Quantity = progressConstructionMaterial.Quantity,
                                Date = progressConstructionMaterial.Date,
                                ProgressConstructionMaterialId = progressConstructionMaterial.Id,
                            });
                        }
                        await importExportInventoryRepository.InsertRange(importExportInventoryHistories);

                        var materialRepository = Resolve<IMaterialRepository>();

                        foreach (var materialId in materialExport.Keys)
                        {
                            var materialDb = await materialRepository.GetById(materialId);
                            if (materialDb.Quantity < materialExport[materialId])
                            {
                                result = BuildAppActionResultError(result, $"Current inventory {materialDb.Name} is {materialDb.Quantity}, lower than demand of  {materialExport[materialId]}");
                                break;
                            }
                            else
                            {
                                materialDb.Quantity -= materialExport[materialId];
                                await materialRepository.Update(materialDb);
                            }
                        }

                        if (!BuildAppActionResultIsError(result))
                        {
                            await _unitOfWork.SaveChangeAsync();
                        }
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

        public async Task<AppActionResult> DeleteProgressConstructionMaterialById(Guid id)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var progressConstructionDb = await _progressConstructionMaterialRepository.GetByExpression(p => p.Id == id, p => p.ExportPriceMaterial);
                    if (progressConstructionDb == null)
                    {
                        result = BuildAppActionResultError(result, $"The progress construction material with {id} not found !");
                    }
                    else
                    {
                        var importExportInventoryRepository = Resolve<IImportExportInventoryHistoryRepository>();
                        var progressHistory = await importExportInventoryRepository.GetByExpression(e => e.ProgressConstructionMaterialId == id);
                        if (progressHistory != null)
                        {
                            await importExportInventoryRepository.DeleteById(progressHistory.Id);
                            var materRepository = Resolve<IMaterialRepository>();
                            var materialDb = await materRepository.GetById(progressConstructionDb.ExportPriceMaterial.MaterialId);
                            materialDb.Quantity += progressHistory.Quantity;
                            await materRepository.Update(materialDb);
                        }
                        await _progressConstructionMaterialRepository.DeleteById(id);
                        await _unitOfWork.SaveChangeAsync();
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
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var progressConstructionMaterialList = await _progressConstructionMaterialRepository.GetAllDataByExpression(null, null);

                    if (progressConstructionMaterialList.Any())
                    {
                        if (pageIndex <= 0) pageIndex = 1;
                        if (pageSize <= 0) pageSize = SD.MAX_RECORD_PER_PAGE;
                        int totalPage = DataPresentationHelper.CalculateTotalPageSize(progressConstructionMaterialList.Count(), pageSize);

                        if (sortInfos != null)
                        {
                            progressConstructionMaterialList = DataPresentationHelper.ApplySorting(progressConstructionMaterialList, sortInfos);
                        }
                        if (pageIndex > 0 && pageSize > 0)
                        {
                            progressConstructionMaterialList = DataPresentationHelper.ApplyPaging(progressConstructionMaterialList, pageIndex, pageSize);
                        }
                        result.Result.Data = progressConstructionMaterialList;
                        result.Result.TotalPage = totalPage;
                    }
                    else
                    {
                        result.Messages.Add("Empty progress construction material list");
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

        public async Task<AppActionResult> GetAllByQuotationDetailId(Guid Id, int pageIndex, int pageSize, IList<SortInfo> sortInfos)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var progressConstructionMaterialList = await _progressConstructionMaterialRepository.GetAllDataByExpression(s => s.QuotationDetailId == Id, null);

                    if (progressConstructionMaterialList.Any())
                    {
                        if (pageIndex <= 0) pageIndex = 1;
                        if (pageSize <= 0) pageSize = SD.MAX_RECORD_PER_PAGE;
                        int totalPage = DataPresentationHelper.CalculateTotalPageSize(progressConstructionMaterialList.Count(), pageSize);

                        if (sortInfos != null)
                        {
                            progressConstructionMaterialList = DataPresentationHelper.ApplySorting(progressConstructionMaterialList, sortInfos);
                        }
                        if (pageIndex > 0 && pageSize > 0)
                        {
                            progressConstructionMaterialList = DataPresentationHelper.ApplyPaging(progressConstructionMaterialList, pageIndex, pageSize);
                        }
                        result.Result.Data = progressConstructionMaterialList;
                        result.Result.TotalPage = totalPage;
                    }
                    else
                    {
                        result.Messages.Add("Empty progress construction material list");
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

        public async Task<AppActionResult> GetAllByQuotationId(Guid Id, int pageIndex, int pageSize, IList<SortInfo> sortInfos)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var progressConstructionMaterialList = await _progressConstructionMaterialRepository.GetAllDataByExpression(p => p.QuotationDetail.QuotationId == Id, null);

                    if (progressConstructionMaterialList.Any())
                    {
                        if (pageIndex <= 0) pageIndex = 1;
                        if (pageSize <= 0) pageSize = SD.MAX_RECORD_PER_PAGE;
                        int totalPage = DataPresentationHelper.CalculateTotalPageSize(progressConstructionMaterialList.Count(), pageSize);

                        if (sortInfos != null)
                        {
                            progressConstructionMaterialList = DataPresentationHelper.ApplySorting(progressConstructionMaterialList, sortInfos);
                        }
                        if (pageIndex > 0 && pageSize > 0)
                        {
                            progressConstructionMaterialList = DataPresentationHelper.ApplyPaging(progressConstructionMaterialList, pageIndex, pageSize);
                        }
                        result.Result.Data = progressConstructionMaterialList;
                        result.Result.TotalPage = totalPage;
                    }
                    else
                    {
                        result.Messages.Add("Empty progress construction material list");
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

        public async Task<AppActionResult> GetProgressConstructionMaterialById(Guid id)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var progressConstructionDb = await _progressConstructionMaterialRepository.GetById(id);
                if (progressConstructionDb != null)
                {
                    result.Result.Data = progressConstructionDb;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetRemainMaterialQuantityForFulfillment(Guid QuotationDetailId)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var quotationDetailRepository = Resolve<IQuotationDetailRepository>();
                var quotationDetailDb = await quotationDetailRepository.GetById(QuotationDetailId);
                if (quotationDetailDb != null)
                {
                    var progressConstructionDb = await _progressConstructionMaterialRepository.GetAllDataByExpression(p => p.QuotationDetailId == QuotationDetailId);
                    int remain = quotationDetailDb.Quantity;
                    progressConstructionDb.ForEach(p => remain -= p.Quantity);
                    if (remain > 0)
                    {
                        result.Result.Data = remain;
                    }
                    else
                    {
                        result.Result.Data = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> UpdateProgressConstructionMaterial(ProgressConstructionMaterialRequest ProgressConstructionMaterialRequest)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var progressConstructionDb = await _progressConstructionMaterialRepository.GetByExpression(p => p.Id == ProgressConstructionMaterialRequest.Id, p => p.InventoryHistory);
                    if (progressConstructionDb == null)
                    {
                        result = BuildAppActionResultError(result, $"The progress construction material with {ProgressConstructionMaterialRequest.Id} not found !");
                    }
                    else
                    {
                        progressConstructionDb.Quantity = ProgressConstructionMaterialRequest.Quantity;
                        var utility = Resolve<Utility>();
                        progressConstructionDb.Date = utility.GetCurrentDateTimeInTimeZone();

                        result.Result.Data = await _progressConstructionMaterialRepository.Update(progressConstructionDb);
                        await _unitOfWork.SaveChangeAsync();
                        if (progressConstructionDb.InventoryHistory != null)
                        {
                            var importExportInventoryRepository = Resolve<IImportExportInventoryHistoryRepository>();
                            var exportInventoryDb = await importExportInventoryRepository.GetById(progressConstructionDb.InventoryHistory.Id);
                            exportInventoryDb.Quantity = ProgressConstructionMaterialRequest.Quantity;
                            exportInventoryDb.Date = utility.GetCurrentDateTimeInTimeZone();
                            await importExportInventoryRepository.Update(exportInventoryDb);
                        }
                        else
                        {
                            result = BuildAppActionResultError(result, $"Inventory History of progress construction material with {ProgressConstructionMaterialRequest.Id} not found");
                        }
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

        private async Task<double> GetDiscountByQuotationDetailId(Guid Id)
        {
            try
            {
                var quotatioDetailRepository = Resolve<IQuotationDetailRepository>();
                var quotatioDetailDb = await quotatioDetailRepository.GetByExpression(q => q.Id == Id, q => q.Material, q => q.Quotation);
                if (quotatioDetailDb != null)
                {
                    if (quotatioDetailDb.Material != null && quotatioDetailDb.Quotation != null)
                    {
                        if (quotatioDetailDb.Material.MaterialType == 0) return quotatioDetailDb.Quotation.RawMaterialDiscount;
                        return quotatioDetailDb.Quotation.FurnitureDiscount;
                    }
                }
                return -1.0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, this);
            }
            return -1.0;
        }
    }
}