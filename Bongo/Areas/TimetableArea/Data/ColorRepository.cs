﻿using Bongo.Areas.TimetableArea.Models;
using Bongo.Data;

namespace Bongo.Areas.TimetableArea.Data
{
    public class ColorRepository : RepositoryBase<Color>, IColorRepository
    {
        public ColorRepository(AppDbContext appDbContext) : base(appDbContext)
        {

        }

        public Color GetByName(string name)
        {
            return _appDbContext.Set<Color>().FirstOrDefault(c=>c.ColorName == name);
        }
    }
}
