@echo off
echo Installing PyInstaller...
pip install pyinstaller

echo.
echo Building executable...
python -m PyInstaller --onefile --windowed --name ExcelToJsonConverter excel_to_json.py

echo.
echo Build complete! Check the 'dist' folder for ExcelToJsonConverter.exe
pause
