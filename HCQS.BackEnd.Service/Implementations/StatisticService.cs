﻿using AutoMapper;
using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.Response;
using HCQS.BackEnd.Common.Util;
using HCQS.BackEnd.DAL.Contracts;
using HCQS.BackEnd.DAL.Models;
using HCQS.BackEnd.Service.Contracts;
using System.Globalization;
using Contract = HCQS.BackEnd.DAL.Models.Contract;

namespace HCQS.BackEnd.Service.Implementations
{
    public class StatisticService : GenericBackendService, IStatisticService
    {
        private BackEndLogger _logger;
        private IMapper _mapper;
        private IAccountRepository _accountRepository;
        private IImportExportInventoryHistoryRepository _importExportInventoryHistoryRepository;
        private IQuotationRepository _quotationRepository;
        private IContractRepository _contractRepository;
        private IProjectRepository _projectRepository;

        public StatisticService(BackEndLogger logger, IMapper mapper, IAccountRepository accountRepository, IImportExportInventoryHistoryRepository importExportInventoryHistoryRepository, IQuotationRepository quotationRepository, IContractRepository contractRepository, IProjectRepository projectRepository, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _logger = logger;
            _mapper = mapper;
            _accountRepository = accountRepository;
            _quotationRepository = quotationRepository;
            _contractRepository = contractRepository;
            _projectRepository = projectRepository;
            _importExportInventoryHistoryRepository = importExportInventoryHistoryRepository;
        }

        public async Task<AppActionResult> GetIncreaseContract(int year = -1, int timePeriod = 1)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                List<Contract> contractDb = new List<Contract>();
                Dictionary<string, int> contractCountPerTimePeriod = new Dictionary<string, int>();
                if (year > 0)
                {
                    contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.Year == year, null);
                    contractCountPerTimePeriod = contractDb
                                                .GroupBy(a => a.DateOfContract.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Count());
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddDays(7) > DateTime.UtcNow && a.DateOfContract <= DateTime.UtcNow, null);
                        contractCountPerTimePeriod = contractDb
                                        .GroupBy(a => a.DateOfContract.Date)
                                        .ToDictionary(group => group.Key.ToString("yyyy-MM-dd"), group => group.Count());
                    }
                    else if (timePeriod == 2)
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddMonths(1) > DateTime.UtcNow && a.DateOfContract <= DateTime.UtcNow, null);
                        contractCountPerTimePeriod = contractDb
                            .GroupBy(a => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(a.DateOfContract, CalendarWeekRule.FirstDay, DayOfWeek.Sunday))
                            .ToDictionary(group => $"Week {group.Key}", group => group.Count());
                    }
                    else if (timePeriod == 3)
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddMonths(6) > DateTime.UtcNow && a.DateOfContract <= DateTime.UtcNow, null);
                        contractCountPerTimePeriod = contractDb
                                                .GroupBy(a => a.DateOfContract.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Count());
                    }
                    else
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddYears(1) > DateTime.UtcNow && a.DateOfContract <= DateTime.UtcNow, null);
                        contractCountPerTimePeriod = contractDb
                                                .GroupBy(a => a.DateOfContract.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Count());
                    }
                }
                result.Result.Data = contractCountPerTimePeriod;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetIncreaseExportInventory(int year = -1, int timePeriod = 1)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> exportDb = new List<ImportExportInventoryHistory>();
                Dictionary<string, double> totalExportPerTimePeriod = new Dictionary<string, double>();
                if (year > 0)
                {
                    exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.ProgressConstructionMaterialId != null
                                                                                                    && a.Date.Year == year,
                                                                                                    e => e.ProgressConstructionMaterial.ExportPriceMaterial);
                    totalExportPerTimePeriod = exportDb
                                                .GroupBy(a => a.Date.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity * a.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - a.ProgressConstructionMaterial.Discount / 100)));
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.ProgressConstructionMaterialId != null
                                                                                                        && a.Date.AddDays(7) > DateTime.UtcNow
                                                                                                        && a.Date <= DateTime.UtcNow,
                                                                                                        e => e.ProgressConstructionMaterial.ExportPriceMaterial);
                        totalExportPerTimePeriod = exportDb
                                        .GroupBy(a => a.Date.Date)
                                        .ToDictionary(group => group.Key.ToString("yyyy-MM-dd"), group => group.Sum(a => a.Quantity * a.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - a.ProgressConstructionMaterial.Discount / 100)));
                    }
                    else if (timePeriod == 2)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.ProgressConstructionMaterialId != null
                                                                                                        && a.Date.Month == DateTime.UtcNow.Month
                                                                                                        && a.Date.Year == DateTime.UtcNow.Year
                                                                                                        && a.Date <= DateTime.UtcNow,
                                                                                                        e => e.ProgressConstructionMaterial.ExportPriceMaterial);
                        totalExportPerTimePeriod = exportDb
                            .GroupBy(a => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(a.Date, CalendarWeekRule.FirstDay, DayOfWeek.Sunday))
                            .ToDictionary(group => $"Week {group.Key}", group => group.Sum(a => a.Quantity * a.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - a.ProgressConstructionMaterial.Discount / 100)));
                    }
                    else if (timePeriod == 3)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.ProgressConstructionMaterialId != null
                                                                                                        && a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                        && a.Date.Year == DateTime.UtcNow.Year
                                                                                                        && a.Date <= DateTime.UtcNow,
                                                                                                        e => e.ProgressConstructionMaterial.ExportPriceMaterial);
                        totalExportPerTimePeriod = exportDb
                                                .GroupBy(a => a.Date.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity * a.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - a.ProgressConstructionMaterial.Discount / 100)));
                    }
                    else
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.ProgressConstructionMaterialId != null
                                                                                                        && a.Date.Year == DateTime.UtcNow.Year
                                                                                                        && a.Date <= DateTime.UtcNow,
                                                                                                        e => e.ProgressConstructionMaterial.ExportPriceMaterial);
                        totalExportPerTimePeriod = exportDb
                                                .GroupBy(a => a.Date.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity * a.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - a.ProgressConstructionMaterial.Discount / 100)));
                    }
                }
                result.Result.Data = totalExportPerTimePeriod;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetIncreaseImportInventory(int year = -1, int timePeriod = 1)
        {
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> importDb = null;
                Dictionary<string, double> totalImportPerTimePeriod = new Dictionary<string, double>();
                if (year > 0)
                {
                    importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.SupplierPriceDetailId != null && a.Date.Year == year, i => i.SupplierPriceDetail);
                    totalImportPerTimePeriod = importDb
                                         .GroupBy(a => a.Date.ToString("MMMM"))
                                         .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity * a.SupplierPriceDetail.Price));
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.SupplierPriceDetailId != null
                                                                                                            && a.Date.AddDays(7) > DateTime.UtcNow
                                                                                                            && a.Date <= DateTime.UtcNow,
                                                                                                            i => i.SupplierPriceDetail);
                        totalImportPerTimePeriod = importDb
                                        .GroupBy(a => a.Date.Date)
                                        .ToDictionary(group => group.Key.ToString("yyyy-MM-dd"), group => group.Sum(a => a.Quantity * a.SupplierPriceDetail.Price));
                    }
                    else if (timePeriod == 2)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.SupplierPriceDetailId != null
                                                                                                        && a.Date.Month == DateTime.UtcNow.Month
                                                                                                        && a.Date.Year == DateTime.UtcNow.Year
                                                                                                        && a.Date <= DateTime.UtcNow,
                                                                                                        i => i.SupplierPriceDetail);
                        totalImportPerTimePeriod = importDb
                            .GroupBy(a => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(a.Date, CalendarWeekRule.FirstDay, DayOfWeek.Sunday))
                            .ToDictionary(group => $"Week {group.Key}", group => group.Sum(a => a.Quantity * a.SupplierPriceDetail.Price));
                    }
                    else if (timePeriod == 3)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.SupplierPriceDetailId != null
                                                                                                        && a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                        && a.Date.Year == DateTime.UtcNow.Year
                                                                                                        && a.Date <= DateTime.UtcNow, i => i.SupplierPriceDetail);
                        totalImportPerTimePeriod = importDb
                                         .GroupBy(a => a.Date.ToString("MMMM"))
                                         .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity * a.SupplierPriceDetail.Price));
                    }
                    else
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.SupplierPriceDetailId != null && a.Date.Year == DateTime.UtcNow.Year && a.Date <= DateTime.UtcNow, i => i.SupplierPriceDetail);
                        totalImportPerTimePeriod = importDb
                                         .GroupBy(a => a.Date.ToString("MMMM"))
                                         .ToDictionary(group => group.Key, group => group.Sum(a => a.Quantity * a.SupplierPriceDetail.Price));
                    }
                }
                result.Result.Data = totalImportPerTimePeriod;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //stopwatch.Stop();
            //result.Messages.Add(stopwatch.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetIncreaseProject(int year = -1, int timePeriod = 1)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                List<Project> projectDb = null;
                Dictionary<string, int> projectCountPerTimePeriod = new Dictionary<string, int>();
                if (year > 0)
                {
                    projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Year == year, null);
                    projectCountPerTimePeriod = projectDb
                                                .GroupBy(a => a.CreateDate.ToString("MMMM"))
                                                .ToDictionary(group => group.Key, group => group.Count());
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.AddDays(7) > DateTime.UtcNow
                                                                                         && a.CreateDate <= DateTime.UtcNow);
                        projectCountPerTimePeriod = projectDb
                                        .GroupBy(a => a.CreateDate.Date)
                                        .ToDictionary(group => group.Key.ToString("yyyy-MM-dd"), group => group.Count());
                    }
                    else if (timePeriod == 2)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Month == DateTime.UtcNow.Month
                                                                                      && a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                      && a.CreateDate <= DateTime.UtcNow);
                        projectCountPerTimePeriod = projectDb
                            .GroupBy(a => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(a.CreateDate, CalendarWeekRule.FirstDay, DayOfWeek.Sunday))
                            .ToDictionary(group => $"Week {group.Key}", group => group.Count());
                    }
                    else if (timePeriod == 3)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Month + 6 > DateTime.UtcNow.Month
                                                                                         && a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                         && a.CreateDate <= DateTime.UtcNow, null);
                        projectCountPerTimePeriod = projectDb
                                                .GroupBy(a => a.CreateDate.ToString("MMM"))
                                                .ToDictionary(group => group.Key, group => group.Count());
                    }
                    else
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                    && a.CreateDate <= DateTime.UtcNow, null);
                        projectCountPerTimePeriod = projectDb
                                                .GroupBy(a => a.CreateDate.ToString("MMM"))
                                                .ToDictionary(group => group.Key, group => group.Count());
                    }
                }
                result.Result.Data = projectCountPerTimePeriod;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalAccount()
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var accountDb = await _accountRepository.GetAllDataByExpression(a => !a.IsDeleted, null);
                if (accountDb != null)
                {
                    result.Result.Data = accountDb.Count();
                }
                else
                {
                    result.Result.Data = 0;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalContract()
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var contractDb = await _contractRepository.GetAllDataByExpression(null, null);
                if (contractDb != null)
                {
                    result.Result.Data = contractDb.Count();
                }
                else
                {
                    result.Result.Data = 0;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalImport()
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(i => i.SupplierPriceDetailId != null, i => i.SupplierPriceDetail);
                if (importDb != null)
                {
                    double value = 0;
                    importDb.ForEach(e => value += e.Quantity * e.SupplierPriceDetail.Price);
                    result.Result.Data = value;
                }
                else
                {
                    result.Result.Data = 0;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalExport()
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(e => e.ProgressConstructionMaterialId != null, e => e.ProgressConstructionMaterial.ExportPriceMaterial);
                if (exportDb != null)
                {
                    double value = 0;
                    exportDb.ForEach(e => value += e.Quantity * e.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - e.ProgressConstructionMaterial.Discount / 100));
                    result.Result.Data = value;
                }
                else
                {
                    result.Result.Data = 0;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalProject()
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var projectDb = await _projectRepository.GetAllDataByExpression(null, null);
                if (projectDb != null)
                {
                    result.Result.Data = projectDb.Count();
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalQuotationRequest()
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var quotationDb = await _quotationRepository.GetAllDataByExpression(null, null);
                if (quotationDb != null)
                {
                    result.Result.Data = quotationDb.Count();
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetAccountRatio()
        {
            //Stopwatch stopwatch = new Stopwatch();
            // Start the stopwatch
            //stopwatch.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<Account> accountDb = await _accountRepository.GetAllDataByExpression(a => !a.IsDeleted, null);
                Dictionary<string, int> accountDistribution = new Dictionary<string, int>();
                if (accountDb != null)
                {
                    var userRoleRepository = Resolve<IUserRoleRepository>();
                    var roleRepository = Resolve<IIdentityRoleRepository>();
                    Dictionary<string, string> roles = new Dictionary<string, string>();
                    var roleDb = await roleRepository.GetAllDataByExpression(null, null);
                    foreach (var role in roleDb)
                    {
                        roles.Add(role.Name, role.Id);
                    }

                    int sumRole = 0;

                    foreach (var account in accountDb)
                    {
                        var userRoles = await userRoleRepository.GetAllDataByExpression(u => u.UserId == account.Id, null);
                        sumRole = 0;
                        userRoles.ForEach(u =>
                        {
                            if (u.RoleId == roles["ADMIN"])
                            {
                                sumRole += 2;
                            }
                            else if (u.RoleId == roles["STAFF"])
                            {
                                sumRole += 1;
                            }
                        });

                        if (sumRole >= 2)
                        {
                            if (accountDistribution.ContainsKey("ADMIN"))
                            {
                                accountDistribution["ADMIN"]++;
                            }
                            else
                            {
                                accountDistribution.Add("ADMIN", 1);
                            }
                        }
                        else if (sumRole >= 1)
                        {
                            if (accountDistribution.ContainsKey("STAFF"))
                            {
                                accountDistribution["STAFF"]++;
                            }
                            else
                            {
                                accountDistribution.Add("STAFF", 1);
                            }
                        }
                        else
                        {
                            if (accountDistribution.ContainsKey("CUSTOMER"))
                            {
                                accountDistribution["CUSTOMER"]++;
                            }
                            else
                            {
                                accountDistribution.Add("CUSTOMER", 1);
                            }
                        }
                    }
                    result.Result.Data = accountDistribution;
                }
                else
                {
                    result.Result.Data = 0;
                }
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //stopwatch.Stop();
            //result.Messages.Add(stopwatch.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetProjectContructionTypeRatio(int year = -1, int timePeriod = 1)
        {
            //Stopwatch stopwatch = new Stopwatch();

            // Start the stopwatch
            //stopwatch.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<Project> projectDb = null;
                if (year > 0)
                {
                    projectDb = await _projectRepository.GetAllDataByExpression(p => p.CreateDate.Year == year, null);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.AddDays(7) > DateTime.UtcNow
                                                                                         && a.CreateDate <= DateTime.UtcNow, null);
                    }
                    else if (timePeriod == 2)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Month == DateTime.UtcNow.Month
                                                                                      && a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                      && a.CreateDate <= DateTime.UtcNow);
                    }
                    else if (timePeriod == 3)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Month + 6 > DateTime.UtcNow.Month
                                                                                         && a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                         && a.CreateDate <= DateTime.UtcNow, null);
                    }
                    else
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Year == DateTime.UtcNow.Year, null);
                    }
                }

                Dictionary<string, int> projectDistribution = new Dictionary<string, int>();
                if (projectDb != null)
                {
                    string key = null;
                    projectDb.ForEach(p =>
                    {
                        key = SD.EnumType.ProjectConstructionType[p.ConstructionType];
                        if (projectDistribution.ContainsKey(key))
                        {
                            projectDistribution[key]++;
                        }
                        else
                        {
                            projectDistribution.Add(key, 1);
                        }
                    });
                }
                result.Result.Data = projectDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //stopwatch.Stop();
            //result.Messages.Add(stopwatch.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetProjectStatusRatio(int year = -1, int timePeriod = 1)

        {
            //Stopwatch stopwatch = new Stopwatch();

            // Start the stopwatch
            //stopwatch.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<Project> projectDb = null;
                if (year > 0)
                {
                    projectDb = await _projectRepository.GetAllDataByExpression(p => p.CreateDate.Year == year, null);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.AddDays(7) > DateTime.UtcNow
                                                                                         && a.CreateDate <= DateTime.UtcNow, null);
                    }
                    else if (timePeriod == 2)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Month == DateTime.UtcNow.Month
                                                                                      && a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                      && a.CreateDate <= DateTime.UtcNow);
                    }
                    else if (timePeriod == 3)
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Month + 6 > DateTime.UtcNow.Month
                                                                                         && a.CreateDate.Year == DateTime.UtcNow.Year
                                                                                         && a.CreateDate <= DateTime.UtcNow, null);
                    }
                    else
                    {
                        projectDb = await _projectRepository.GetAllDataByExpression(a => a.CreateDate.Year == DateTime.UtcNow.Year, null);
                    }
                }

                Dictionary<string, int> projectDistribution = new Dictionary<string, int>();
                if (projectDb != null)
                {
                    string key = null;
                    projectDb.ForEach(p =>
                    {
                        key = SD.EnumType.ProjectStatus[p.Status];
                        if (projectDistribution.ContainsKey(key))
                        {
                            projectDistribution[key]++;
                        }
                        else
                        {
                            projectDistribution.Add(key, 1);
                        }
                    });
                }
                result.Result.Data = projectDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //stopwatch.Stop();
            //result.Messages.Add(stopwatch.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetContractRatio(int year = -1, int timePeriod = 1)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                List<Contract> contractDb = null;
                if (year > 0)
                {
                    contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.Year == year, null);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddDays(7) > DateTime.UtcNow, null);
                    }
                    else if (timePeriod == 2)
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddMonths(1) > DateTime.UtcNow, null);
                    }
                    else if (timePeriod == 3)
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddMonths(6) > DateTime.UtcNow, null);
                    }
                    else
                    {
                        contractDb = await _contractRepository.GetAllDataByExpression(a => a.DateOfContract.AddYears(1) > DateTime.UtcNow, null);
                    }
                }

                Dictionary<string, int> contractDistribution = new Dictionary<string, int>();
                if (contractDb != null)
                {
                    string key = null;
                    contractDb.ForEach(c =>
                    {
                        key = SD.EnumType.ContractStatus[c.ContractStatus];
                        if (contractDistribution.ContainsKey(key))
                        {
                            contractDistribution[key]++;
                        }
                        else
                        {
                            contractDistribution.Add(key, 1);
                        }
                    });
                }
                result.Result.Data = contractDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }

        public async Task<AppActionResult> GetTotalImportBySupplierRatio(int year = -1, int timePeriod = 1)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            //sw.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> importDb = null;
                if (year > 0)
                {
                    importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == year
                                                                                                        && a.SupplierPriceDetailId != null,
                                                                                                        a => a.SupplierPriceDetail.SupplierPriceQuotation.Supplier);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.AddDays(7) > DateTime.UtcNow
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.SupplierPriceDetailId != null,
                                                                                                         a => a.SupplierPriceDetail.SupplierPriceQuotation.Supplier);
                    }
                    else if (timePeriod == 2)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month == DateTime.UtcNow.Month
                                                                                                          && a.Date.Year == DateTime.UtcNow.Year
                                                                                                          && a.Date <= DateTime.UtcNow
                                                                                                          && a.SupplierPriceDetailId != null,
                                                                                                          a => a.SupplierPriceDetail.SupplierPriceQuotation.Supplier);
                    }
                    else if (timePeriod == 3)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                             && a.Date.Year == DateTime.UtcNow.Year
                                                                                                             && a.Date <= DateTime.UtcNow
                                                                                                             && a.SupplierPriceDetailId != null,
                                                                                                             a => a.SupplierPriceDetail.SupplierPriceQuotation.Supplier);
                    }
                    else
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == DateTime.UtcNow.Year && a.SupplierPriceDetailId != null, a => a.SupplierPriceDetail.SupplierPriceQuotation.Supplier);
                    }
                }

                Dictionary<string, double> importDistribution = new Dictionary<string, double>();
                if (importDb != null)
                {
                    string key = null;
                    importDb.ForEach(i =>
                    {
                        key = i.SupplierPriceDetail.SupplierPriceQuotation.Supplier.SupplierName;
                        if (importDistribution.ContainsKey(key))
                        {
                            importDistribution[key] += i.Quantity * i.SupplierPriceDetail.Price;
                        }
                        else
                        {
                            importDistribution.Add(key, i.Quantity * i.SupplierPriceDetail.Price);
                        }
                    });
                }
                result.Result.Data = importDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //sw.Stop();
            //result.Messages.Add(sw.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetTotalImportByMaterialRatio(int year = -1, int timePeriod = 1)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            //sw.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> importDb = null;
                if (year > 0)
                {
                    importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == year
                                                                                                        && a.SupplierPriceDetailId != null,
                                                                                                        a => a.SupplierPriceDetail.Material);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.AddDays(7) > DateTime.UtcNow
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.SupplierPriceDetailId != null,
                                                                                                         a => a.SupplierPriceDetail.Material);
                    }
                    else if (timePeriod == 2)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month == DateTime.UtcNow.Month
                                                                                                          && a.Date.Year == DateTime.UtcNow.Year
                                                                                                          && a.Date <= DateTime.UtcNow
                                                                                                          && a.SupplierPriceDetailId != null, a => a.SupplierPriceDetail.Material);
                    }
                    else if (timePeriod == 3)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                         && a.Date.Year == DateTime.UtcNow.Year
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.SupplierPriceDetailId != null,
                                                                                                         a => a.SupplierPriceDetail.Material);
                    }
                    else
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == DateTime.UtcNow.Year && a.SupplierPriceDetailId != null, a => a.SupplierPriceDetail.Material);
                    }
                }

                Dictionary<string, double> exportDistribution = new Dictionary<string, double>();
                if (importDb != null)
                {
                    string key = null;
                    importDb.ForEach(i =>
                    {
                        key = i.SupplierPriceDetail.Material.Name;
                        if (exportDistribution.ContainsKey(key))
                        {
                            exportDistribution[key] += i.Quantity * i.SupplierPriceDetail.Price;
                        }
                        else
                        {
                            exportDistribution.Add(key, i.Quantity * i.SupplierPriceDetail.Price);
                        }
                    });
                }
                result.Result.Data = exportDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //sw.Stop();
            //result.Messages.Add(sw.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetTotalExportByMaterialRatio(int year = -1, int timePeriod = 1)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            //sw.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> exportDb = null;
                if (year > 0)
                {
                    exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == year
                                                                                                        && a.ProgressConstructionMaterialId != null,
                                                                                                        a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.AddDays(7) > DateTime.UtcNow
                                                                                         && a.Date <= DateTime.UtcNow
                                                                                        && a.ProgressConstructionMaterialId != null,
                                                                                        a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                    else if (timePeriod == 2)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month == DateTime.UtcNow.Month
                                                                                                          && a.Date.Year == DateTime.UtcNow.Year
                                                                                                          && a.Date <= DateTime.UtcNow
                                                                                                          && a.ProgressConstructionMaterialId != null, a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                    else if (timePeriod == 3)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                         && a.Date.Year == DateTime.UtcNow.Year
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.ProgressConstructionMaterialId != null,
                                                                                                         a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                    else
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == DateTime.UtcNow.Year && a.ProgressConstructionMaterialId != null, a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                }

                Dictionary<string, double> exportDistribution = new Dictionary<string, double>();
                if (exportDb != null)
                {
                    string key = null;
                    exportDb.ForEach(i =>
                    {
                        key = i.ProgressConstructionMaterial.ExportPriceMaterial.Material.Name;
                        if (exportDistribution.ContainsKey(key))
                        {
                            exportDistribution[key] += i.Quantity * i.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - i.ProgressConstructionMaterial.Discount / 100);
                        }
                        else
                        {
                            exportDistribution.Add(key, i.Quantity * i.ProgressConstructionMaterial.ExportPriceMaterial.Price * (1 - i.ProgressConstructionMaterial.Discount / 100));
                        }
                    });
                }
                result.Result.Data = exportDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //sw.Stop();
            //result.Messages.Add(sw.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetQuantityImportByMaterialRatio(int year = -1, int timePeriod = 1)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            //sw.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> importDb = null;
                if (year > 0)
                {
                    importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == year
                                                                                                        && a.SupplierPriceDetailId != null,
                                                                                                        a => a.SupplierPriceDetail.Material);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.AddDays(7) > DateTime.UtcNow
                                                                                                             && a.Date <= DateTime.UtcNow
                                                                                                            && a.SupplierPriceDetailId != null,
                                                                                                            a => a.SupplierPriceDetail.Material);
                    }
                    else if (timePeriod == 2)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month == DateTime.UtcNow.Month
                                                                                                          && a.Date.Year == DateTime.UtcNow.Year
                                                                                                          && a.Date <= DateTime.UtcNow
                                                                                                          && a.SupplierPriceDetailId != null, a => a.SupplierPriceDetail.Material);
                    }
                    else if (timePeriod == 3)
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                         && a.Date.Year == DateTime.UtcNow.Year
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.SupplierPriceDetailId != null,
                                                                                                         a => a.SupplierPriceDetail.Material);
                    }
                    else
                    {
                        importDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == DateTime.UtcNow.Year && a.SupplierPriceDetailId != null, a => a.SupplierPriceDetail.Material);
                    }
                }

                Dictionary<string, double> exportDistribution = new Dictionary<string, double>();
                if (importDb != null)
                {
                    string key = null;
                    importDb.ForEach(i =>
                    {
                        key = i.SupplierPriceDetail.Material.Name;
                        if (exportDistribution.ContainsKey(key))
                        {
                            exportDistribution[key] += i.Quantity;
                        }
                        else
                        {
                            exportDistribution.Add(key, i.Quantity);
                        }
                    });
                }
                result.Result.Data = exportDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //sw.Stop();
            //result.Messages.Add(sw.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetQuantityExportByMaterialRatio(int year = -1, int timePeriod = 1)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            //sw.Start();
            AppActionResult result = new AppActionResult();
            try
            {
                List<ImportExportInventoryHistory> exportDb = null;
                if (year > 0)
                {
                    exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == year
                                                                                                        && a.ProgressConstructionMaterialId != null,
                                                                                                        a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                }
                else
                {
                    year = DateTime.Now.Year;
                    if (timePeriod == 1)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.AddDays(7) > DateTime.UtcNow
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.ProgressConstructionMaterialId != null,
                                                                                                         a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                    else if (timePeriod == 2)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month == DateTime.UtcNow.Month
                                                                                                          && a.Date.Year == DateTime.UtcNow.Year
                                                                                                          && a.Date <= DateTime.UtcNow
                                                                                                          && a.ProgressConstructionMaterialId != null,
                                                                                                          a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                    else if (timePeriod == 3)
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Month + 6 > DateTime.UtcNow.Month
                                                                                                         && a.Date.Year == DateTime.UtcNow.Year
                                                                                                         && a.Date <= DateTime.UtcNow
                                                                                                         && a.ProgressConstructionMaterialId != null,
                                                                                                         a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                    else
                    {
                        exportDb = await _importExportInventoryHistoryRepository.GetAllDataByExpression(a => a.Date.Year == DateTime.UtcNow.Year
                                                                                                        && a.ProgressConstructionMaterialId != null,
                                                                                                        a => a.ProgressConstructionMaterial.ExportPriceMaterial.Material);
                    }
                }

                Dictionary<string, double> exportDistribution = new Dictionary<string, double>();
                if (exportDb != null)
                {
                    string key = null;
                    exportDb.ForEach(i =>
                    {
                        key = i.ProgressConstructionMaterial.ExportPriceMaterial.Material.Name;
                        if (exportDistribution.ContainsKey(key))
                        {
                            exportDistribution[key] += i.Quantity;
                        }
                        else
                        {
                            exportDistribution.Add(key, i.Quantity);
                        }
                    });
                }
                result.Result.Data = exportDistribution;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            //sw.Stop();
            //result.Messages.Add(sw.Elapsed.ToString());
            return result;
        }

        public async Task<AppActionResult> GetIncreaseImportExportInventory(int year = -1, int timePeriod = 1)
        {
            AppActionResult result = new AppActionResult();
            try
            {
                var increaseImportDb = await GetIncreaseImportInventory(year, timePeriod);
                var increaseExportDb = await GetIncreaseExportInventory(year, timePeriod);
                var increaseImport = (Dictionary<string, double>)increaseImportDb.Result.Data;
                var increaseExport = (Dictionary<string, double>)increaseExportDb.Result.Data;
                var keySet = new HashSet<string>();
                if (increaseImport != null)
                {
                    foreach (var key in increaseImport.Keys)
                    {
                        keySet.Add(key);
                    }
                }
                else
                {
                    increaseImport = new Dictionary<string, double>();
                }
                if (increaseExport != null)
                {
                    foreach (var key in increaseExport.Keys)
                    {
                        keySet.Add(key);
                    }
                }
                else
                {
                    increaseExport = new Dictionary<string, double>();
                }
                List<ImportExportIncreaseResponse> data = new List<ImportExportIncreaseResponse>();
                bool importExist = false;
                bool exportExist = false;
                foreach (var key in keySet)
                {
                    importExist = increaseImport.TryGetValue(key, out double importValue);
                    exportExist = increaseExport.TryGetValue(key, out double exportValue);
                    data.Add(
                        new ImportExportIncreaseResponse
                        {
                            Date = key,
                            ExportIncrease = exportExist ? exportValue : 0,
                            ImportIncrease = importExist ? importValue : 0
                        });
                }
                result.Result.Data = data;
            }
            catch (Exception ex)
            {
                result = BuildAppActionResultError(result, ex.Message);
                _logger.LogError(ex.Message, this);
            }
            return result;
        }
    }
}