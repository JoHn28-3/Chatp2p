@echo off
cd /d "%~dp0"
git status -s
set /p MSG="Message du commit (Entree pour defaut): "
if "%MSG%"=="" set MSG=Mise a jour ChatP2P Desktop
git add -A
git commit -m "%MSG%"
git pull --rebase origin master
git push -u origin master
pause
