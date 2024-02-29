﻿using static HCQS.BackEnd.Common.Dto.Request.ProjectDto;

namespace HCQS.BackEnd.Common.Dto.Request
{
    public class ConstructionConfigRequest
    {
        public Guid Id { get; set; }

        public double SandMixingRatio { get; set; }
        public double CementMixingRatio { get; set; }
        public double StoneMixingRatio { get; set; }

        public ConstructionType ConstructionType { get; set; }

        public string NumOfFloor { get; set; }
        public string Area { get; set; }
        public string TiledArea { get; set; }
    }
}