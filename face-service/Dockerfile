FROM python:3.10-slim
WORKDIR /app
COPY requirements.txt .
# face-recognition declares the source-only dlib package. dlib-bin provides the
# same Python module through a pinned prebuilt wheel, so install its thin API
# package without resolving that source dependency again.
RUN pip install --no-cache-dir -r requirements.txt \
    && pip install --no-cache-dir --no-deps face-recognition==1.3.0
COPY . .
EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
