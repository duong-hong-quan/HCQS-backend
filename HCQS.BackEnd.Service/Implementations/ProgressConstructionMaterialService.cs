﻿using AutoMapper;
using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.BaseRequest;
using HCQS.BackEnd.Common.Dto.Request;
using HCQS.BackEnd.DAL.Contracts;
using HCQS.BackEnd.DAL.Implementations;
using HCQS.BackEnd.DAL.Models;
using HCQS.BackEnd.DAL.Util;
using HCQS.BackEnd.Service.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace HCQS.BackEnd.Service.Implementations
{
    public class ProgressConstructionMaterialService:GenericBackendService, IProgressConstructionMaterialService
    {
        private BackEndLogger _logger;
        private IUnitOfWork _unitOfWork;
        private IProgressConstructionMaterialRepository _progressConstructionMaterialRepository;
        private IMapper _mapper;
        public ProgressConstructionMaterialService(BackEndLogger logger, IUnitOfWork unitOfWork, IProgressConstructionMaterialRepository progressConstructionMaterialRepository, IMapper mapper, IServiceProvider service):base(service)
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
                    List<ProgressConstructionMaterial> progressConstructionMaterials = new List<ProgressConstructionMaterial>();
                    foreach (var ProgressConstructionMaterialRequest in ProgressConstructionMaterialRequests)
                    {
                        //get material => get latest price => 
                        var quotationDetailDb = await quotationDetailRepository.GetByExpression(q => q.Id == ProgressConstructionMaterialRequest.QuotationDetailId, q => q.Material);
                        if(quotationDetailDb != null)
                        {
                            var exportMaterialPrices = await exportPriceRepository.GetAllDataByExpression(e => e.MaterialId == quotationDetailDb.MaterialId);
                            var latestMaterialPrice = exportMaterialPrices.OrderByDescending(e => e.Date).FirstOrDefault();
                            var discount = await GetDiscountByQuotationDetailId(quotationDetailDb.Id);
                            var newProgressConstructionMaterial = new ProgressConstructionMaterial
                            {
                                Id = Guid.NewGuid(),
                                Discount = discount,
                                Date = ProgressConstructionMaterialRequest.Date,
                                Quantity = ProgressConstructionMaterialRequest.Quantity,
                                Total = ProgressConstructionMaterialRequest.Quantity * (1 - discount) * latestMaterialPrice.Price,
                                
                                ExportPriceMaterialId = latestMaterialPrice.Id,
                                QuotationDetailId = quotationDetailDb.Id
                            };
                            progressConstructionMaterials.Add(newProgressConstructionMaterial);
                        } else
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
                        foreach(var progressConstructionMaterial in progressConstructionMaterials)
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
                        await _unitOfWork.SaveChangeAsync();
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

        public async Task<AppActionResult> DeleteProgressConstructionMaterialById(Guid id)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var supplierDb = await _progressConstructionMaterialRepository.GetById(id);
                    if (supplierDb == null)
                    {
                        result = BuildAppActionResultError(result, $"The supplier with {id} not found !");
                    }
                    else
                    {
                        result.Result.Data = await _progressConstructionMaterialRepository.DeleteById(id);
                        await _unitOfWork.SaveChangeAsync();
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
                        result.Messages.Add("Empty supplier list");
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
                        result.Messages.Add("Empty supplier list");
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
                        result.Messages.Add("Empty supplier list");
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

        public async Task<AppActionResult> GetProgressConstructionMaterialById(Guid id)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var supplierDb = await _progressConstructionMaterialRepository.GetById(id);
                if (supplierDb != null)
                {
                    result.Result.Data = supplierDb;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, SD.ResponseMessage.INTERNAL_SERVER_ERROR, true);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> UpdateProgressConstructionMaterial(Guid Id,ProgressConstructionMaterialRequest ProgressConstructionMaterialRequest)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                AppActionResult result = new AppActionResult();
                try
                {
                    var supplierDb = await _progressConstructionMaterialRepository.GetByExpression(p => p.Id == Id, p => p.InventoryHistory);
                    if (supplierDb == null)
                    {
                        result = BuildAppActionResultError(result, $"The progress construction material with {Id} not found !");
                    }
                    else
                    {
                        supplierDb.Quantity = ProgressConstructionMaterialRequest.Quantity;
                        supplierDb.Date = ProgressConstructionMaterialRequest.Date;

                        result.Result.Data = await _progressConstructionMaterialRepository.Update(supplierDb);
                        await _unitOfWork.SaveChangeAsync();
                        if(supplierDb.InventoryHistory != null)
                        {
                            var importExportInventoryRepository = Resolve<IImportExportInventoryHistoryRepository>();
                            var exportInventoryDb = await importExportInventoryRepository.GetById(supplierDb.InventoryHistory.Id);
                            exportInventoryDb.Quantity = ProgressConstructionMaterialRequest.Quantity;
                            exportInventoryDb.Date = ProgressConstructionMaterialRequest.Date;
                            await importExportInventoryRepository.Update(exportInventoryDb);
                        } else
                        {
                            result = BuildAppActionResultError(result, $"Inventory History of progress construction material with {Id} not found");
                        }
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
                            if(quotatioDetailDb.Material.MaterialType == 0) return quotatioDetailDb.Quotation.RawMaterialDiscount;
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