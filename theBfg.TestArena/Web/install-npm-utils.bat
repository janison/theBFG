@echo install nodeJS first
@echo then install http://msysgit.github.io/
@echo then the rest of the .bat commands will work

npm i nvm
nvm install 11.15.0
nvm use 11.15.0
npm install -g grunt-cli grunt bower yo
npm install
bower install