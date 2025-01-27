﻿using HCQS.BackEnd.Common.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HCQS.BackEnd.Service.Contracts
{
    public interface IFileService
    {
        public IActionResult ConvertDataToExcel();

        public ActionResult<List<List<string>>> UploadExcel(IFormFile file);

        public IActionResult GenerateExcelContent<T>(IEnumerable<T> dataList, string sheetName);

        public IActionResult GenerateTemplateExcel<T>(T dataList);

        public IActionResult ReturnErrorColored<T>(List<string> headers, List<List<string>> data, Dictionary<int, string> rowsToColor, string filename);

        public Task<AppActionResult> UploadFileToFirebase(IFormFile file, string pathFileName);

        public Task<AppActionResult> DeleteFileFromFirebase(string pathFileName);

        public IFormFile ConvertHtmlToPdf(string content, string fileName);
    }
}