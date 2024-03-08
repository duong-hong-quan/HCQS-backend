﻿using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.Request;

namespace HCQS.BackEnd.Service.Contracts
{
    public interface IConstructionConfigValueService
    {
        Task<AppActionResult> CreateConstructionConfig(ConstructionConfigRequest request);

        Task<AppActionResult> UpdateConstructionConfig(ConstructionConfigRequest request);

        Task<AppActionResult> DeleteConstructionConfig(FilterConstructionConfigRequest request);

        Task<AppActionResult> DeleteConstructionConfigById(Guid Id);

        Task<AppActionResult> DeleteAllConstructionConfig();

        Task<AppActionResult> GetConstructionConfig(SearchConstructionConfigRequest request);

        Task<AppActionResult> GetMaxConfig();

        Task<AppActionResult> SearchConstructionConfig(FilterConstructionConfigRequest request);

        Task<AppActionResult> GetAll();
    }
}